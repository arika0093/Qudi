using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

/// <summary>
/// Generates dispatch-based composite implementations.
/// </summary>
internal static class CompositeDispatchCodeGenerator
{
    private const string Task = "global::System.Threading.Tasks.Task";
    private const string NotSupportedException = "global::System.NotSupportedException";
    private const string IEnumerable = "global::System.Collections.Generic.IEnumerable";

    public static void AppendDispatchCompositeImplementation(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target
    )
    {
        AppendOpenGenericCompositeSupport(builder, target);
        AppendConstraintDispatchers(builder, target);
    }

    private static void AppendOpenGenericCompositeSupport(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target
    )
    {
        var genericArgs = target.GenericTypeArguments;
        var genericParams = target.GenericTypeParameters;
        var whereClause = string.IsNullOrEmpty(genericParams) ? "" : $" {genericParams}";

        var typeName = string.IsNullOrEmpty(genericArgs)
            ? target.ImplementingTypeName
            : $"{target.ImplementingTypeName}{genericArgs}";

        var helperName = HelperCodeGeneratorUtility.BuildHelperInterfaceName(
            target.InterfaceHelperName,
            isComposite: true
        );
        var helperInterfaceName = string.IsNullOrEmpty(genericArgs)
            ? helperName
            : $"{helperName}{genericArgs}";

        var helperTypeName = string.IsNullOrEmpty(target.ImplementingTypeNamespace)
            ? helperInterfaceName
            : $"global::{target.ImplementingTypeNamespace}.{helperInterfaceName}";

        var useNamespace = !string.IsNullOrEmpty(target.ImplementingTypeNamespace);
        builder.AppendLineIf(useNamespace, $"namespace {target.ImplementingTypeNamespace}");
        using (builder.BeginScopeIf(useNamespace))
        {
            var containingTypes = target.ContainingTypes.ToArray();
            foreach (var containingType in containingTypes)
            {
                builder.AppendLine(
                    $"{containingType.Accessibility} partial {containingType.TypeKeyword} {containingType.Name}"
                );
                builder.AppendLine("{");
                builder.IncreaseIndent();
            }

            builder.AppendLine(
                $"{target.ImplementingTypeAccessibility} partial {target.ImplementingTypeKeyword} {typeName} : {helperTypeName}{whereClause}"
            );
            using (builder.BeginScope())
            {
                builder.AppendLine(
                    $"private readonly {IEnumerable}<{target.InterfaceName}> __innerServices;"
                );
                builder.AppendLine("");
                builder.AppendLine(
                    $"{target.ImplementingTypeAccessibility} {target.ImplementingTypeName}({IEnumerable}<{target.InterfaceName}> innerServices)"
                );
                using (builder.BeginScope())
                {
                    builder.AppendLine("__innerServices = innerServices;");
                }

                builder.AppendLine("");
                builder.AppendLine(CodeTemplateContents.EditorBrowsableAttribute);
                builder.AppendLine(
                    $"{IEnumerable}<{target.InterfaceName}> {helperTypeName}.__InnerServices => __innerServices;"
                );
            }

            for (var i = 0; i < containingTypes.Length; i++)
            {
                builder.DecreaseIndent();
                builder.AppendLine("}");
            }
        }
    }

    private static void AppendConstraintDispatchers(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target
    )
    {
        if (target.ConstraintTypes.Count == 0)
        {
            return;
        }

        foreach (var constraint in target.ConstraintTypes)
        {
            AppendDispatcherForConstraint(builder, target, constraint);
        }
    }

    private static void AppendDispatcherForConstraint(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        DispatchCompositeConstraintType constraint
    )
    {
        var className = $"{target.ImplementingTypeName}__Dispatch_{constraint.Suffix}";
        var useNamespace = !string.IsNullOrEmpty(target.ImplementingTypeNamespace);
        builder.AppendLineIf(useNamespace, $"namespace {target.ImplementingTypeNamespace}");
        using (builder.BeginScopeIf(useNamespace))
        {
            builder.AppendLine(
                $"internal sealed class {className} : {constraint.ConstructedInterfaceTypeName}"
            );
            using (builder.BeginScope())
            {
                foreach (var concreteType in target.ConcreteTypes)
                {
                    builder.AppendLine(
                        $"private readonly {IEnumerable}<{concreteType.ConstructedInterfaceTypeName}> {concreteType.FieldName};"
                    );
                }

                builder.AppendLine("");
                builder.AppendLine(
                    $"public {className}({BuildConstructorParameters(target)})"
                );
                using (builder.BeginScope())
                {
                    foreach (var concreteType in target.ConcreteTypes)
                    {
                        builder.AppendLine(
                            $"{concreteType.FieldName} = {concreteType.ParameterName};"
                        );
                    }
                }

                foreach (var method in target.Methods)
                {
                    builder.AppendLine("");
                    AppendDispatchMethod(builder, target, method, constraint);
                }
            }
        }
    }

    private static string BuildConstructorParameters(DispatchCompositeTarget target)
    {
        return string.Join(
            ", ",
            target.ConcreteTypes.Select(t =>
                $"{IEnumerable}<{t.ConstructedInterfaceTypeName}> {t.ParameterName}"
            )
        );
    }

    private static void AppendDispatchMethod(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        DispatchCompositeMethod method,
        DispatchCompositeConstraintType constraint
    )
    {
        var member = method.Member;
        var returnType = ReplaceTypeParameter(
            member.ReturnTypeName,
            target.GenericTypeArguments,
            constraint.TypeName
        );
        var parameters = BuildParameterListWithReplacement(
            member.Parameters,
            target.GenericTypeArguments,
            constraint.TypeName
        );

        builder.AppendLine($"public {returnType} {member.Name}({parameters})");
        using (builder.BeginScope())
        {
            if (method.DispatchParameterIndex < 0)
            {
                builder.AppendLine(
                    $"throw new {NotSupportedException}(\"{member.Name} is not supported in this dispatch composite.\");"
                );
                return;
            }

            var dispatchParamName = member.Parameters[method.DispatchParameterIndex].Name;
            var dispatchArguments = BuildDispatchArguments(
                member.Parameters,
                method.DispatchParameterIndex,
                "__arg"
            );

            var isVoid = returnType == "void";
            var isBool = returnType == "bool";
            var isTask = returnType == Task;
            var isEnumerable =
                returnType.Contains("IEnumerable")
                || returnType.Contains("ICollection")
                || returnType.Contains("IList");

            if (!isVoid && !isBool && !isTask && !isEnumerable)
            {
                builder.AppendLine(
                    $"throw new {NotSupportedException}(\"{member.Name} is not supported in this dispatch composite.\");"
                );
                return;
            }

            if (isVoid)
            {
                AppendDispatchVoid(builder, target, member.Name, dispatchArguments, dispatchParamName);
                return;
            }

            if (isBool)
            {
                AppendDispatchBool(builder, target, member.Name, dispatchArguments, dispatchParamName);
                return;
            }

            if (isTask)
            {
                AppendDispatchTask(builder, target, member.Name, dispatchArguments, dispatchParamName);
                return;
            }

            AppendDispatchEnumerable(
                builder,
                target,
                member.Name,
                dispatchArguments,
                dispatchParamName,
                returnType
            );
        }
    }

    private static void AppendDispatchVoid(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        string methodName,
        string arguments,
        string dispatchParamName
    )
    {
        builder.AppendLine($"switch ({dispatchParamName})");
        using (builder.BeginScope())
        {
            foreach (var concrete in target.ConcreteTypes)
            {
                builder.AppendLine($"case {concrete.TypeName} __arg:");
                builder.IncreaseIndent();
                builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                using (builder.BeginScope())
                {
                    builder.AppendLine($"__validator.{methodName}({arguments});");
                }
                builder.AppendLine("break;");
                builder.DecreaseIndent();
            }

            builder.AppendLine("default:");
            builder.IncreaseIndent();
            builder.AppendLine("break;");
            builder.DecreaseIndent();
        }
    }

    private static void AppendDispatchBool(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        string methodName,
        string arguments,
        string dispatchParamName
    )
    {
        builder.AppendLine($"switch ({dispatchParamName})");
        using (builder.BeginScope())
        {
            foreach (var concrete in target.ConcreteTypes)
            {
                builder.AppendLine($"case {concrete.TypeName} __arg:");
                builder.IncreaseIndent();
                builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                using (builder.BeginScope())
                {
                    builder.AppendLine($"if (!__validator.{methodName}({arguments})) return false;");
                }
                builder.AppendLine("return true;");
                builder.DecreaseIndent();
            }

            builder.AppendLine("default:");
            builder.IncreaseIndent();
            builder.AppendLine("return true;");
            builder.DecreaseIndent();
        }
    }

    private static void AppendDispatchTask(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        string methodName,
        string arguments,
        string dispatchParamName
    )
    {
        builder.AppendLine($"switch ({dispatchParamName})");
        using (builder.BeginScope())
        {
            foreach (var concrete in target.ConcreteTypes)
            {
                builder.AppendLine($"case {concrete.TypeName} __arg:");
                builder.IncreaseIndent();
                builder.AppendLine($"var __tasks = new global::System.Collections.Generic.List<{Task}>();");
                builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                using (builder.BeginScope())
                {
                    builder.AppendLine($"__tasks.Add(__validator.{methodName}({arguments}));");
                }
                builder.AppendLine($"return {Task}.WhenAll(__tasks);");
                builder.DecreaseIndent();
            }

            builder.AppendLine("default:");
            builder.IncreaseIndent();
            builder.AppendLine($"return {Task}.CompletedTask;");
            builder.DecreaseIndent();
        }
    }

    private static void AppendDispatchEnumerable(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        string methodName,
        string arguments,
        string dispatchParamName,
        string returnType
    )
    {
        var elementType = ExtractEnumerableType(returnType);
        builder.AppendLine($"switch ({dispatchParamName})");
        using (builder.BeginScope())
        {
            foreach (var concrete in target.ConcreteTypes)
            {
                builder.AppendLine($"case {concrete.TypeName} __arg:");
                builder.IncreaseIndent();
                builder.AppendLine(
                    $"var __results = new global::System.Collections.Generic.List<{elementType}>();"
                );
                builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                using (builder.BeginScope())
                {
                    builder.AppendLine($"var __serviceResult = __validator.{methodName}({arguments});");
                    builder.AppendLine("if (__serviceResult != null)");
                    using (builder.BeginScope())
                    {
                        builder.AppendLine("__results.AddRange(__serviceResult);");
                    }
                }
                builder.AppendLine("return __results;");
                builder.DecreaseIndent();
            }

            builder.AppendLine("default:");
            builder.IncreaseIndent();
            builder.AppendLine(
                $"return new global::System.Collections.Generic.List<{elementType}>();"
            );
            builder.DecreaseIndent();
        }
    }

    private static string ExtractEnumerableType(string enumerableType)
    {
        var match = Regex.Match(enumerableType, @"<([^<>]+)>$");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return "object";
    }

    private static string BuildDispatchArguments(
        EquatableArray<HelperParameter> parameters,
        int dispatchIndex,
        string replacement
    )
    {
        var list = new List<string>();
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            var name = i == dispatchIndex ? replacement : parameter.Name;
            var prefix = parameter.RefKindPrefix ?? string.Empty;
            list.Add($"{prefix}{name}");
        }

        return string.Join(", ", list);
    }

    private static string BuildParameterListWithReplacement(
        EquatableArray<HelperParameter> parameters,
        string typeParamName,
        string replacementTypeName
    )
    {
        return string.Join(
            ", ",
            parameters.Select(parameter =>
            {
                var typeName = ReplaceTypeParameter(
                    parameter.TypeName,
                    typeParamName,
                    replacementTypeName
                );
                var prefix = parameter.RefKindPrefix ?? string.Empty;
                var paramsPrefix = parameter.IsParams ? "params " : string.Empty;
                return $"{paramsPrefix}{typeName} {prefix}{parameter.Name}";
            })
        );
    }

    private static string ReplaceTypeParameter(
        string text,
        string typeParamName,
        string replacementTypeName
    )
    {
        if (string.IsNullOrEmpty(typeParamName))
        {
            return text;
        }

        var typeParam = typeParamName.Trim('<', '>', ' ');
        if (string.IsNullOrEmpty(typeParam))
        {
            return text;
        }

        var pattern = $@"\b{Regex.Escape(typeParam)}\b";
        return Regex.Replace(text, pattern, replacementTypeName);
    }
}
