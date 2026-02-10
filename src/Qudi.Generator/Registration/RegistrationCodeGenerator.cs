using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Dependency;

namespace Qudi.Generator.Registration;

internal static class RegistrationCodeGenerator
{
    private const string TRInfo = "global::Qudi.TypeRegistrationInfo";
    private const string List = "global::System.Collections.Generic.List";
    private const string IReadOnlyList = "global::System.Collections.Generic.IReadOnlyList";
    private const string TRList = $"{List}<{TRInfo}>";
    private const string TRResult = $"{IReadOnlyList}<{TRInfo}>";
    private const string VisitedHashSet = "global::System.Collections.Generic.HashSet<long>";

    public static void GenerateAddQudiServicesCode(
        SourceProductionContext context,
        ImmutableArray<RegistrationSpec?> registrations,
        ProjectInfo projectInfo
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
        // Qudi.Generated__HASH1234.QudiRegistrations.FetchAll ...
        GenerateQudiRegistrationsCodes(builder, regs, projectInfo);

        var source = builder.ToString();
        context.AddSource("Qudi.Registration.g.cs", source);
    }

    private static void GenerateQudiInternalRegistrationsCode(
        IndentedStringBuilder builder,
        ProjectInfo projectInfo
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

    private static void GenerateQudiRegistrationsCodes(
        IndentedStringBuilder builder,
        ImmutableArray<RegistrationSpec> registrations,
        ProjectInfo projectInfo
    )
    {
        builder.AppendLine($"namespace Qudi.Generated__{projectInfo.ProjectHash}");
        builder.AppendLine("{");
        builder.IncreaseIndent();
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
            // export WithDependencies (contains all project's dependencies)
            GenerateWithDependenciesCode(builder, projectInfo);
            builder.AppendLine("");
            // export Self (contains only this project's registrations)
            GenerateSelfCode(builder);
            builder.AppendLine("");
            // export Original (all registrations defined in this project)
            GenerateOriginalFieldCode(builder, registrations, projectInfo);
        }
        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void GenerateWithDependenciesCode(
        IndentedStringBuilder builder,
        ProjectInfo projectInfo
    )
    {
        builder.AppendLine(
            $"""
            /// <summary>
            /// Gets all registrations including dependencies. This method is used internally for Qudi.
            /// </summary>
            /// <param name="fromOther">Whether to include only public registrations from other projects.</param>
            /// <returns>All registrations including dependencies.</returns>
            public static void WithDependencies({TRList} collection, {VisitedHashSet} visited, bool fromOther)
            """
        );
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
            foreach (var dep in projectInfo.Dependencies)
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
        ProjectInfo projectInfo
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
                var markAsStrategyLiteral = reg.MarkAsStrategy ? "true" : "false";
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
                        MarkAsStrategy = {{markAsStrategyLiteral}},
                        AssemblyName = "{{projectInfo.AssemblyName}}",
                        Namespace = "{{reg.Namespace}}",
                    },
                    """
                );
            }
        }
    }
}
