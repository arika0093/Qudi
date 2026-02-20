using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Qudi.Generator;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

/// <summary>
/// Main orchestrator for generating helper interfaces and partial classes for decorator and composite patterns.
/// </summary>
internal static class HelperCodeGenerator
{
    private const string IEnumerable = "global::System.Collections.Generic.IEnumerable";

    public static void GenerateHelpers(SourceProductionContext context, HelperGenerationInput input)
    {
        var interfaceTargets = input.InterfaceTargets;
        var implementingTargets = input.ImplementingTargets;
        var dispatchTargets = input.DispatchCompositeTargets;

        if (
            interfaceTargets.Count == 0
            && implementingTargets.Count == 0
            && dispatchTargets.Count == 0
        )
        {
            return;
        }
        if (interfaceTargets.Count > 0)
        {
            var builder = new IndentedStringBuilder();
            builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
            foreach (var helper in interfaceTargets)
            {
                GenerateInterfaceWithNamespace(builder, helper);
                builder.AppendLine("");
            }

            context.AddSource("Qudi.Helper.Interfaces.g.cs", builder.ToString());
        }

        if (implementingTargets.Count > 0 || dispatchTargets.Count > 0)
        {
            var builder = new IndentedStringBuilder();
            builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
            foreach (var target in implementingTargets)
            {
                GeneratePartialClass(builder, target);
                builder.AppendLine("");
            }
            foreach (var target in dispatchTargets)
            {
                CompositeDispatchCodeGenerator.AppendDispatchCompositeImplementation(
                    builder,
                    target
                );
                builder.AppendLine("");
            }

            context.AddSource("Qudi.Helper.Classes.g.cs", builder.ToString());
        }
    }

    // generate partial class for implementing target
    private static void GeneratePartialClass(
        IndentedStringBuilder builder,
        HelperImplementingTarget target
    )
    {
        var helperName = HelperCodeGeneratorUtility.BuildHelperInterfaceName(
            target.InterfaceHelperName,
            target.IsComposite
        );
        var genericArgs = target.GenericTypeArguments;
        var interfaceGenericArgs = target.InterfaceGenericTypeArguments;
        var genericParams = target.GenericTypeParameters;

        var helperInterfaceName = string.IsNullOrEmpty(interfaceGenericArgs)
            ? helperName
            : $"{helperName}{interfaceGenericArgs}";

        var helperTypeName = string.IsNullOrEmpty(target.InterfaceNamespace)
            ? helperInterfaceName
            : $"global::{target.InterfaceNamespace}.{helperInterfaceName}";

        var implementingTypeName = string.IsNullOrEmpty(genericArgs)
            ? target.ImplementingTypeName
            : $"{target.ImplementingTypeName}{genericArgs}";

        // Build where clauses if present
        var whereClause = string.IsNullOrEmpty(genericParams) ? "" : $" {genericParams}";

        // namespace
        var useNamespace = !string.IsNullOrEmpty(target.ImplementingTypeNamespace);
        builder.AppendLineIf(useNamespace, $"namespace {target.ImplementingTypeNamespace}");
        using (builder.BeginScopeIf(useNamespace))
        {
            // Open containing (parent) types if this is a nested class
            var containingTypes = target.ContainingTypes.ToArray();
            foreach (var containingType in containingTypes)
            {
                builder.AppendLine(
                    $"{containingType.Accessibility} partial {containingType.TypeKeyword} {containingType.Name}"
                );
                builder.AppendLine("{");
                builder.IncreaseIndent();
            }

            if (target.UseIntercept)
            {
                builder.AppendLine(
                    $$"""
                    partial {{target.ImplementingTypeKeyword}} {{implementingTypeName}} : {{helperTypeName}}{{whereClause}}
                    {
                        private {{helperTypeName}}.__BaseImpl Base => __baseCache ??= new({{target.BaseParameterName}}, this);

                        {{CodeTemplateContents.EditorBrowsableAttribute}}
                        private {{helperTypeName}}.__BaseImpl? __baseCache;

                        {{CodeTemplateContents.EditorBrowsableAttribute}}
                        {{helperTypeName}}.__BaseImpl {{helperTypeName}}.__Base => Base;
                    }
                    """
                );
            }
            else if (target.IsComposite)
            {
                builder.AppendLine(
                    $"partial {target.ImplementingTypeKeyword} {implementingTypeName} : {helperTypeName}{whereClause}"
                );
                using (builder.BeginScope())
                {
                    builder.AppendLine(CodeTemplateContents.EditorBrowsableAttribute);
                    builder.AppendLine(
                        $"global::System.Collections.Generic.IEnumerable<{target.InterfaceName}> {helperTypeName}.__InnerServices => {target.BaseParameterName};"
                    );

                    foreach (var compositeMethod in target.CompositeMethodOverrides)
                    {
                        CompositeCodeGenerator.AppendCompositePartialMethodImplementation(
                            builder,
                            compositeMethod,
                            target.BaseParameterName
                        );
                    }
                }
            }
            else
            {
                builder.AppendLine(
                    $$"""
                    partial {{target.ImplementingTypeKeyword}} {{implementingTypeName}} : {{helperTypeName}}{{whereClause}}
                    {
                        {{CodeTemplateContents.EditorBrowsableAttribute}}
                        {{target.InterfaceName}} {{helperTypeName}}.__Inner => {{target.BaseParameterName}};
                    }
                    """
                );
            }

            // Close containing types
            for (var i = 0; i < containingTypes.Length; i++)
            {
                builder.DecreaseIndent();
                builder.AppendLine("}");
            }
        }
    }

    // generate interface with namespace
    private static void GenerateInterfaceWithNamespace(
        IndentedStringBuilder builder,
        HelperInterfaceTarget helper
    )
    {
        var namespaceName = helper.InterfaceNamespace;
        var isUseNamespace = !string.IsNullOrEmpty(namespaceName);
        builder.AppendLineIf(isUseNamespace, $"namespace {namespaceName}");
        using (builder.BeginScopeIf(isUseNamespace))
        {
            GenerateInterfaceCore(builder, helper);
        }
    }

    // generate interface core
    private static void GenerateInterfaceCore(
        IndentedStringBuilder builder,
        HelperInterfaceTarget helper
    )
    {
        var interfaceName = helper.InterfaceName;
        var interfaceAccessibility = string.IsNullOrWhiteSpace(helper.InterfaceAccessibility)
            ? "internal"
            : helper.InterfaceAccessibility;
        var interfaceHelperName = helper.InterfaceHelperName;
        var members = helper.Members.ToImmutableArray();
        var useIntercept = helper.UseIntercept;
        var isComposite = helper.IsComposite;
        var helperAccessor = useIntercept ? "__Base" : "__Inner";
        if (!useIntercept && isComposite)
        {
            helperAccessor = "__InnerServices";
        }
        var helperName = HelperCodeGeneratorUtility.BuildHelperInterfaceName(
            interfaceHelperName,
            isComposite
        );
        var genericArgs = helper.GenericTypeArguments;
        var genericParams = helper.GenericTypeParameters;

        // Generate interface declaration with generic type parameters
        // Note: interfaceName is already fully qualified and includes generic arguments if present
        var helperInterfaceName = string.IsNullOrEmpty(genericArgs)
            ? helperName
            : $"{helperName}{genericArgs}";

        // Build the interface declaration line
        var interfaceDeclaration =
            $"{interfaceAccessibility} interface {helperInterfaceName} : {interfaceName}";

        // Add where clauses if present
        if (!string.IsNullOrEmpty(genericParams))
        {
            interfaceDeclaration += $" {genericParams}";
        }

        builder.AppendLine(
            $$"""
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            {{interfaceDeclaration}}
            """
        );
        using (builder.BeginScope())
        {
            // each property / method
            foreach (var member in members)
            {
                if (member.Kind == HelperMemberKind.Method)
                {
                    if (isComposite)
                    {
                        CompositeCodeGenerator.AppendCompositeMethod(
                            builder,
                            member,
                            helperAccessor
                        );
                    }
                    else
                    {
                        DecoratorCodeGenerator.AppendDecoratorMethod(
                            builder,
                            member,
                            helperAccessor
                        );
                    }
                }
                else if (member.Kind == HelperMemberKind.Property)
                {
                    if (isComposite)
                    {
                        CompositeCodeGenerator.AppendCompositeProperty(builder, member);
                    }
                    else
                    {
                        DecoratorCodeGenerator.AppendDecoratorProperty(
                            builder,
                            member,
                            helperAccessor
                        );
                    }
                }
            }
            builder.AppendLine("");

            // base impl class and intercept method
            if (useIntercept)
            {
                builder.AppendLine(
                    $$"""
                    {{IEnumerable}}<bool> Intercept(string methodName, object?[] args)
                    {
                        yield return true;
                    }

                    {{CodeTemplateContents.EditorBrowsableAttribute}}
                    protected __BaseImpl __Base { get; }

                    {{CodeTemplateContents.EditorBrowsableAttribute}}
                    protected class __BaseImpl({{interfaceName}} __Service, {{helperName}} __Root)
                    """
                );
                using (builder.BeginScope())
                {
                    foreach (var member in members)
                    {
                        if (member.Kind == HelperMemberKind.Method)
                        {
                            DecoratorCodeGenerator.AppendBaseImplMethod(builder, member);
                        }
                        else if (member.Kind == HelperMemberKind.Property)
                        {
                            DecoratorCodeGenerator.AppendBaseImplProperty(builder, member);
                        }
                    }
                }
            }
            else if (isComposite)
            {
                builder.AppendLine(
                    $$"""
                    {{IEnumerable}}<{{interfaceName}}> {{helperAccessor}} { get; }
                    """
                );
            }
            else
            {
                builder.AppendLine(
                    $$"""
                    {{interfaceName}} {{helperAccessor}} { get; }
                    """
                );
            }
        }
    }
}
