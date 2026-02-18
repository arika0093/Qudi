using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Dependency;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Registration;

internal static class RegistrationCodeGenerator
{
    private const string TRInfo = "global::Qudi.TypeRegistrationInfo";
    private const string List = "global::System.Collections.Generic.List";
    private const string IReadOnlyList = "global::System.Collections.Generic.IReadOnlyList";
    private const string TRList = $"{List}<{TRInfo}>";
    private const string TRResult = $"{IReadOnlyList}<{TRInfo}>";
    private const string VisitedHashSet = "global::System.Collections.Generic.HashSet<long>";
    private const string WithDependenciesDeclare =
        $"public static partial void WithDependencies({TRList} collection, {VisitedHashSet} visited, bool fromOther)";

    /// <summary>
    /// Generates the internal and self registrations file (depends only on registrations and basicInfo).
    /// Includes Internal, Self, Original, and partial WithDependencies declaration.
    /// </summary>
    public static void GenerateInternalAndSelfRegistrationsFile(
        SourceProductionContext context,
        ImmutableArray<RegistrationSpec?> registrations,
        ProjectBasicInfo projectInfo
    )
    {
        var builder = new IndentedStringBuilder();
        var regs = registrations.Where(r => r is not null).Select(r => r!).ToImmutableArray();

        builder.AppendLine(
            $$"""
            {{CodeTemplateContents.CommonGeneratedHeader}}
            using System.Linq;

            namespace Qudi.Generated
            """
        );

        using (builder.BeginScope())
        {
            // Qudi.Generated.QudiInternalRegistrations.FetchAll ...
            GenerateQudiInternalRegistrationsCode(builder, projectInfo);
        }

        builder.AppendLine("");
        builder.AppendLine($"namespace Qudi.Generated__{projectInfo.ProjectHash}");
        using (builder.BeginScope())
        {
            builder.AppendLine(
                $$"""
                /// <summary>
                /// Contains Qudi registration information for this project.
                /// </summary>
                {{CodeTemplateContents.EditorBrowsableAttribute}}
                public static partial class QudiRegistrations
                """
            );
            using (builder.BeginScope())
            {
                // Partial declaration of WithDependencies (implementation in separate file)
                GenerateWithDependenciesDeclaration(builder);
                builder.AppendLine("");
                // export Self (contains only this project's registrations)
                GenerateSelfCode(builder);
                builder.AppendLine("");
                // export Original (all registrations defined in this project)
                GenerateOriginalFieldCode(builder, regs, projectInfo);
            }
        }

        var source = builder.ToString();
        context.AddSource("Qudi.Registration.Self.g.cs", source);
    }

    /// <summary>
    /// Generates the WithDependencies implementation file (depends on projectInfo and dependencies).
    /// </summary>
    public static void GenerateWithDependenciesImplementationFile(
        SourceProductionContext context,
        ProjectInfo projectInfo
    )
    {
        var builder = new IndentedStringBuilder();
        var projectData = projectInfo.Basic;
        var dependencies = projectInfo.Dependencies;

        builder.AppendLine(
            $$"""
            {{CodeTemplateContents.CommonGeneratedHeader}}
            using System.Linq;

            namespace Qudi.Generated__{{projectData.ProjectHash}}
            """
        );

        using (builder.BeginScope())
        {
            builder.AppendLine("public static partial class QudiRegistrations");
            using (builder.BeginScope())
            {
                // export WithDependencies implementation
                GenerateWithDependenciesImplementation(builder, projectData, dependencies);
            }
        }

        var source = builder.ToString();
        context.AddSource("Qudi.Registration.Dependencies.g.cs", source);
    }

    /// <summary>
    /// Generates the internal registrations file (depends only on projectInfo).
    /// </summary>
    public static void GenerateInternalRegistrationsFile(
        SourceProductionContext context,
        ProjectBasicInfo projectInfo
    )
    {
        var builder = new IndentedStringBuilder();

        builder.AppendLine(
            $$"""
            {{CodeTemplateContents.CommonGeneratedHeader}}
            using System.Linq;

            namespace Qudi.Generated
            """
        );

        using (builder.BeginScope())
        {
            // Qudi.Generated.QudiInternalRegistrations.FetchAll ...
            GenerateQudiInternalRegistrationsCode(builder, projectInfo);
        }

        var source = builder.ToString();
        context.AddSource("Qudi.Registration.Internal.g.cs", source);
    }

    private static void GenerateQudiInternalRegistrationsCode(
        IndentedStringBuilder builder,
        ProjectBasicInfo projectInfo
    )
    {
        builder.AppendLine(
            $$"""
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            internal static partial class QudiInternalRegistrations
            {
                public static {{TRResult}} FetchAll(bool selfOnly = false)
                {
                    var collection = new {{TRList}} { };
                    if (selfOnly)
                    {
                        global::Qudi.Generated__{{projectInfo.ProjectHash}}.QudiRegistrations.Self(
                            collection: collection,
                            fromOther: false
                        );
                    }
                    else
                    {
                        global::Qudi.Generated__{{projectInfo.ProjectHash}}.QudiRegistrations.WithDependencies(
                            collection: collection,
                            visited: new {{VisitedHashSet}} { },
                            fromOther: false
                        );
                    }
                    return collection;
                }
            }
            """
        );
    }

    private static void GenerateWithDependenciesDeclaration(IndentedStringBuilder builder)
    {
        builder.AppendLine(
            $"""
            /// <summary>
            /// Gets all registrations including dependencies. This method is used internally for Qudi.
            /// </summary>
            /// <param name="collection">Collection to add registrations to.</param>
            /// <param name="visited">Set of visited project hashes to avoid cycles.</param>
            /// <param name="fromOther">Whether to include only public registrations from other projects.</param>
            {WithDependenciesDeclare};
            """
        );
    }

    private static void GenerateWithDependenciesImplementation(
        IndentedStringBuilder builder,
        ProjectBasicInfo projectInfo,
        EquatableArray<ProjectDependencyInfo> dependencies
    )
    {
        builder.AppendLine(WithDependenciesDeclare);
        using (builder.BeginScope())
        {
            // add self registrations
            // check visited to avoid duplicate registrations
            builder.AppendLine(
                $$"""
                if (!visited.Add(0x{{projectInfo.ProjectHash}})) return;
                Self(collection, fromOther: fromOther);
                """
            );
            foreach (var dep in dependencies)
            {
                builder.AppendLine(
                    $"global::Qudi.Generated__{dep.ProjectHash}.QudiRegistrations.WithDependencies(collection, visited, fromOther: true);"
                );
            }
        }
    }

    private static void GenerateSelfCode(IndentedStringBuilder builder)
    {
        builder.AppendLine(
            $$"""
            /// <summary>
            /// Gets registrations defined in this project only. This method is used internally for Qudi.
            /// </summary>
            /// <param name="fromOther">Whether to include only public registrations from other projects.</param>
            /// <returns>Registrations defined in this project only.</returns>
            public static void Self({{TRList}} collection, bool fromOther = false)
            {
                collection.AddRange(Original.Where(t => t.UsePublic || !fromOther));
            }
            """
        );
    }

    private static void GenerateOriginalFieldCode(
        IndentedStringBuilder builder,
        ImmutableArray<RegistrationSpec> registrations,
        ProjectBasicInfo projectInfo
    )
    {
        builder.AppendLine(
            $$"""
            /// <summary>
            /// All registrations defined in this project.
            /// </summary>
            private static readonly {{TRList}} Original = new {{TRList}}
            """
        );
        using (builder.BeginScope(start: "{", end: "};"))
        {
            foreach (var reg in registrations)
            {
                var when = string.Join(", ", reg.When.Select(t => $"\"{t}\""));
                var requiredTypes = string.Join(", ", reg.RequiredTypes);
                var asTypes = string.Join(", ", reg.AsTypes);
                var usePublicLiteral = reg.UsePublic ? "true" : "false";
                var markAsDecoratorLiteral = reg.MarkAsDecorator ? "true" : "false";
                var markAsCompositeLiteral = reg.MarkAsComposite ? "true" : "false";
                var markAsDispatcherLiteral = reg.MarkAsDispatcher ? "true" : "false";
                var exportLiteral = reg.Export ? "true" : "false";
                builder.AppendLine(
                    $$"""
                    new {{TRInfo}}
                    {
                        Type = typeof({{reg.TypeName}}),
                        Lifetime = "{{reg.Lifetime}}",
                        When = new {{List}}<string> { {{when}} },
                        RequiredTypes = new {{List}}<global::System.Type> { {{requiredTypes}} },
                        AsTypes = new {{List}}<global::System.Type> { {{asTypes}} },
                        UsePublic = {{usePublicLiteral}},
                        Key = {{(reg.KeyLiteral is null ? "null" : reg.KeyLiteral)}},
                        Order = {{reg.Order}},
                        MarkAsDecorator = {{markAsDecoratorLiteral}},
                        MarkAsComposite = {{markAsCompositeLiteral}},
                        MarkAsDispatcher = {{markAsDispatcherLiteral}},
                        Export = {{exportLiteral}},
                        AssemblyName = "{{projectInfo.AssemblyName}}",
                        Namespace = "{{reg.Namespace}}",
                    },
                    """
                );
            }
        }
    }
}
