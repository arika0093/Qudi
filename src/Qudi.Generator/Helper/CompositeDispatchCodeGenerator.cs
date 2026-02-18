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
        var constraint = target.ConstraintTypes.FirstOrDefault();
        if (constraint is null)
        {
            return;
        }

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
                $"{target.ImplementingTypeAccessibility} partial {target.ImplementingTypeKeyword} {target.ImplementingTypeName}"
            );
            using (builder.BeginScope())
            {
                // recieved dependencies for each concrete type,
                // either as the service itself or as an enumerable if multiple.
                foreach (var concreteType in target.ConcreteTypes)
                {
                    builder.AppendLine(
                        $"private readonly {BuildDispatchDependencyType(concreteType.ConstructedInterfaceTypeName, target.Multiple)} {concreteType.FieldName};"
                    );
                }
                builder.AppendLine("");

                // constructor with dependencies for each concrete type.
                builder.AppendLine(
                    $"{target.ImplementingTypeAccessibility} {target.ImplementingTypeName}({BuildConstructorParameters(target)})"
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

                // each dispatch method, which switches on the dispatch parameter to call the appropriate concrete implementation(s).
                foreach (var method in target.Methods)
                {
                    builder.AppendLine("");
                    AppendDispatchMethod(builder, target, method, constraint);
                }
            }

            for (var i = 0; i < containingTypes.Length; i++)
            {
                builder.DecreaseIndent();
                builder.AppendLine("}");
            }
        }
    }

    private static string BuildConstructorParameters(DispatchCompositeTarget target)
    {
        return string.Join(
            ", ",
            target.ConcreteTypes.Select(t =>
                $"{BuildDispatchDependencyType(t.ConstructedInterfaceTypeName, target.Multiple)} {t.ParameterName}"
            )
        );
    }

    private static string BuildDispatchDependencyType(string serviceTypeName, bool multiple)
    {
        return multiple ? $"{IEnumerable}<{serviceTypeName}>" : serviceTypeName;
    }

    private static void AppendDispatchMethod(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        DispatchCompositeMethod method,
        DispatchCompositeConstraintType constraint
    )
    {
        // Replace the generic type parameter with the constraint type for signatures.
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

        var overrideBehavior = TryGetCompositeOverride(target, method);

        // TODO: support async Task methods that use Any/All behavior (requires changing method signature to return Task<bool> or similar, or adding a separate method for this behavior).
        var asyncModifier =
            returnType == Task && overrideBehavior == CompositeResultBehavior.Sequential
                ? "async "
                : string.Empty;
        var isPartialRequired = overrideBehavior.HasValue;
        var partialModifier = isPartialRequired ? "partial " : string.Empty;
        builder.AppendLine(
            $"public {partialModifier}{asyncModifier}{returnType} {member.Name}({parameters})"
        );
        using (builder.BeginScope())
        {
            if (method.DispatchParameterIndex < 0)
            {
                // No dispatch parameter: dispatch composites require a T parameter to select a target.
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
            var isEnumerable = IsSupportedEnumerableReturnType(returnType);

            if (!isVoid && !isBool && !isTask && !isEnumerable)
            {
                // Keep return types narrow for predictable, AOT-safe generated code.
                builder.AppendLine(
                    $"throw new {NotSupportedException}(\"{member.Name} is not supported in this dispatch composite.\");"
                );
                return;
            }

            if (isVoid)
            {
                AppendDispatchVoid(
                    builder,
                    target,
                    member.Name,
                    dispatchArguments,
                    dispatchParamName
                );
                return;
            }

            if (isBool)
            {
                var useAny =
                    overrideBehavior.HasValue
                    && overrideBehavior.Value == CompositeResultBehavior.Any;
                AppendDispatchBool(
                    builder,
                    target,
                    member.Name,
                    dispatchArguments,
                    dispatchParamName,
                    useAny
                );
                return;
            }

            if (isTask)
            {
                var behavior = overrideBehavior ?? CompositeResultBehavior.All;
                AppendDispatchTask(
                    builder,
                    target,
                    member.Name,
                    dispatchArguments,
                    dispatchParamName,
                    behavior
                );
                return;
            }

            // CompositeMethod overrides do not apply to enumerable returns (same as composite behavior).
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
                if (target.Multiple)
                {
                    builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                    using (builder.BeginScope())
                    {
                        builder.AppendLine($"__validator.{methodName}({arguments});");
                    }
                }
                else
                {
                    builder.AppendLine($"{concrete.FieldName}.{methodName}({arguments});");
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
        string dispatchParamName,
        bool useAny
    )
    {
        builder.AppendLine($"switch ({dispatchParamName})");
        using (builder.BeginScope())
        {
            foreach (var concrete in target.ConcreteTypes)
            {
                builder.AppendLine($"case {concrete.TypeName} __arg:");
                builder.IncreaseIndent();
                if (target.Multiple)
                {
                    builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                    using (builder.BeginScope())
                    {
                        if (useAny)
                        {
                            builder.AppendLine(
                                $"if (__validator.{methodName}({arguments})) return true;"
                            );
                        }
                        else
                        {
                            builder.AppendLine(
                                $"if (!__validator.{methodName}({arguments})) return false;"
                            );
                        }
                    }
                    builder.AppendLine(useAny ? "return false;" : "return true;");
                }
                else
                {
                    builder.AppendLine($"return {concrete.FieldName}.{methodName}({arguments});");
                }
                builder.DecreaseIndent();
            }

            builder.AppendLine("default:");
            builder.IncreaseIndent();
            builder.AppendLine(useAny ? "return false;" : "return true;");
            builder.DecreaseIndent();
        }
    }

    private static void AppendDispatchTask(
        IndentedStringBuilder builder,
        DispatchCompositeTarget target,
        string methodName,
        string arguments,
        string dispatchParamName,
        CompositeResultBehavior behavior
    )
    {
        builder.AppendLine($"switch ({dispatchParamName})");
        using (builder.BeginScope())
        {
            foreach (var concrete in target.ConcreteTypes)
            {
                builder.AppendLine($"case {concrete.TypeName} __arg:");
                builder.IncreaseIndent();
                if (target.Multiple)
                {
                    if (behavior == CompositeResultBehavior.Sequential)
                    {
                        builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                        using (builder.BeginScope())
                        {
                            builder.AppendLine($"await __validator.{methodName}({arguments});");
                        }
                        builder.AppendLine("return;");
                    }
                    else
                    {
                        builder.AppendLine(
                            $"var __tasks = new global::System.Collections.Generic.List<{Task}>();"
                        );
                        builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                        using (builder.BeginScope())
                        {
                            builder.AppendLine(
                                $"__tasks.Add(__validator.{methodName}({arguments}));"
                            );
                        }
                        if (behavior == CompositeResultBehavior.Any)
                        {
                            builder.AppendLine($"await {Task}.WhenAny(__tasks);");
                            builder.AppendLine("return;");
                        }
                        else
                        {
                            builder.AppendLine($"return {Task}.WhenAll(__tasks);");
                        }
                    }
                }
                else
                {
                    if (
                        behavior == CompositeResultBehavior.Any
                        || behavior == CompositeResultBehavior.Sequential
                    )
                    {
                        builder.AppendLine(
                            $"await {concrete.FieldName}.{methodName}({arguments});"
                        );
                        builder.AppendLine("return;");
                    }
                    else
                    {
                        builder.AppendLine(
                            $"return {concrete.FieldName}.{methodName}({arguments});"
                        );
                    }
                }
                builder.DecreaseIndent();
            }

            builder.AppendLine("default:");
            builder.IncreaseIndent();
            builder.AppendLine(
                behavior == CompositeResultBehavior.Sequential
                    ? "return;"
                    : $"return {Task}.CompletedTask;"
            );
            builder.DecreaseIndent();
        }
    }

    private static CompositeResultBehavior? TryGetCompositeOverride(
        DispatchCompositeTarget target,
        DispatchCompositeMethod method
    )
    {
        foreach (var overrideMethod in target.CompositeMethodOverrides)
        {
            if (
                !string.Equals(
                    overrideMethod.Name,
                    method.Member.Name,
                    System.StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            if (overrideMethod.Parameters.Count != method.Member.Parameters.Count)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < overrideMethod.Parameters.Count; i++)
            {
                var left = overrideMethod.Parameters[i];
                var right = method.Member.Parameters[i];
                if (!string.Equals(left.TypeName, right.TypeName, System.StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
                if (
                    !string.Equals(
                        left.RefKindPrefix,
                        right.RefKindPrefix,
                        System.StringComparison.Ordinal
                    )
                )
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            return overrideMethod.ResultBehavior;
        }

        return null;
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
                if (target.Multiple)
                {
                    builder.AppendLine($"foreach (var __validator in {concrete.FieldName})");
                    using (builder.BeginScope())
                    {
                        builder.AppendLine(
                            $"var __serviceResult = __validator.{methodName}({arguments});"
                        );
                        builder.AppendLine("if (__serviceResult != null)");
                        using (builder.BeginScope())
                        {
                            builder.AppendLine("__results.AddRange(__serviceResult);");
                        }
                    }
                }
                else
                {
                    builder.AppendLine(
                        $"var __serviceResult = {concrete.FieldName}.{methodName}({arguments});"
                    );
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
        var openIndex = enumerableType.IndexOf('<');
        if (openIndex < 0)
        {
            return "object";
        }

        var depth = 0;
        for (var i = openIndex; i < enumerableType.Length; i++)
        {
            if (enumerableType[i] == '<')
            {
                depth++;
            }
            else if (enumerableType[i] == '>')
            {
                depth--;
                if (depth == 0)
                {
                    return enumerableType.Substring(openIndex + 1, i - openIndex - 1);
                }
            }
        }

        return "object";
    }

    private static bool IsSupportedEnumerableReturnType(string returnType)
    {
        var openIndex = returnType.IndexOf('<');
        var typeName = openIndex >= 0 ? returnType.Substring(0, openIndex) : returnType;
        return typeName
            is "global::System.Collections.Generic.IEnumerable"
                or "global::System.Collections.Generic.ICollection"
                or "global::System.Collections.Generic.IList";
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
                var refKindPrefix = parameter.RefKindPrefix ?? string.Empty;
                var paramsPrefix = parameter.IsParams ? "params " : string.Empty;
                return $"{paramsPrefix}{refKindPrefix}{typeName} {parameter.Name}";
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
