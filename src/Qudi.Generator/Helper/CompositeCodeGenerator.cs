using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

/// <summary>
/// Generates helper code for composite pattern implementations.
/// </summary>
internal static class CompositeCodeGenerator
{
    private const string Task = "global::System.Threading.Tasks.Task";
    private const string NotSupportedException = "global::System.NotSupportedException";

    public static void AppendCompositeMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor
    )
    {
        var returnType = method.ReturnTypeName;
        var parameters = HelperCodeGeneratorUtility.BuildParameterList(method.Parameters);
        var arguments = HelperCodeGeneratorUtility.BuildArgumentList(method.Parameters);
        var interfaceName = method.DeclaringInterfaceName;

        // Determine return type category
        var isVoid = returnType == "void";
        var isBool = returnType == "bool";
        var isTask = returnType == Task;
        var isIEnumerable =
            returnType.Contains("IEnumerable")
            || returnType.Contains("ICollection")
            || returnType.Contains("IList");

        // For composite, we iterate over all inner services and call the method on each
        var asyncModifier = isTask ? "async " : "";
        builder.AppendLine($"{asyncModifier}{returnType} {interfaceName}.{method.Name}({parameters})");
        using (builder.BeginScope())
        {
            if (isVoid)
            {
                AppendCompositeVoidMethod(builder, method, helperAccessor, arguments);
            }
            else if (isBool)
            {
                // Default to CompositeResult.All (logical AND) for bool
                AppendCompositeBoolMethod(builder, method, helperAccessor, arguments, useAnd: true);
            }
            else if (isTask)
            {
                AppendCompositeTaskSequentialMethod(builder, method, helperAccessor, arguments);
            }
            else if (isIEnumerable)
            {
                AppendCompositeEnumerableMethod(
                    builder,
                    method,
                    helperAccessor,
                    arguments,
                    returnType
                );
            }
            else
            {
                AppendCompositeUnsupportedMethod(builder, method.Name);
            }
        }
    }

    public static void AppendCompositeProperty(IndentedStringBuilder builder, HelperMember property)
    {
        var typeName = property.ReturnTypeName;
        var propertyName = property.IsIndexer ? "this" : property.Name;
        var parameters = property.IsIndexer
            ? HelperCodeGeneratorUtility.BuildParameterList(property.Parameters)
            : "";
        var indexerSuffix = property.IsIndexer ? $"[{parameters}]" : "";
        var interfaceName = property.DeclaringInterfaceName;

        builder.AppendLine($"{typeName} {interfaceName}.{propertyName}{indexerSuffix}");
        using (builder.BeginScope())
        {
            if (property.HasGetter)
            {
                builder.AppendLine(
                    $"get => throw new {NotSupportedException}(\"{propertyName} is not supported in this composite.\");"
                );
            }
            if (property.HasSetter)
            {
                builder.AppendLine(
                    $"set => throw new {NotSupportedException}(\"{propertyName} is not supported in this composite.\");"
                );
            }
        }
    }

    public static void AppendCompositePartialMethodImplementation(
        IndentedStringBuilder builder,
        CompositeMethodOverride method,
        string innerServicesAccessor
    )
    {
        var returnType = method.ReturnTypeName;
        var parameters = HelperCodeGeneratorUtility.BuildParameterList(method.Parameters);
        var arguments = HelperCodeGeneratorUtility.BuildArgumentList(method.Parameters);
        var isVoid = returnType == "void";
        var isBool = returnType == "bool";
        var isTask = returnType == Task;

        // Task composite methods always execute sequentially.
        var asyncModifier = isTask ? "async " : "";

        builder.AppendLine(
            $"public partial {asyncModifier}{returnType} {method.Name}({parameters})"
        );
        using (builder.BeginScope())
        {
            if (isVoid)
            {
                AppendCompositeVoidMethodBody(
                    builder,
                    method.Name,
                    innerServicesAccessor,
                    arguments
                );
                return;
            }

            if (!string.IsNullOrEmpty(method.ResultAggregator))
            {
                if (isTask || isVoid)
                {
                    AppendCompositeUnsupportedMethod(builder, method.Name);
                    return;
                }

                AppendCompositeAggregateMethodBody(
                    builder,
                    method.Name,
                    innerServicesAccessor,
                    arguments,
                    method.ReturnTypeName,
                    method.ResultAggregator
                );
                return;
            }

            if (isBool)
            {
                var useAnd = method.ResultBehavior != CompositeResultBehavior.Any;
                AppendCompositeBoolMethodBody(
                    builder,
                    method.Name,
                    innerServicesAccessor,
                    arguments,
                    useAnd
                );
                return;
            }

            if (isTask)
            {
                AppendCompositeTaskSequentialMethodBody(
                    builder,
                    method.Name,
                    innerServicesAccessor,
                    arguments
                );
                return;
            }

            AppendCompositeUnsupportedMethod(builder, method.Name);
        }
    }

    private static void AppendCompositeVoidMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor,
        string arguments
    )
    {
        // Fire-and-forget for void methods
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"__service.{method.Name}({arguments});");
        }
    }

    private static void AppendCompositeBoolMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor,
        string arguments,
        bool useAnd
    )
    {
        if (useAnd)
        {
            // CompositeResult.All - logical AND
            builder.AppendLine($"var __result = true;");
            builder.AppendLine($"foreach (var __service in {helperAccessor})");
            using (builder.BeginScope())
            {
                builder.AppendLine($"__result = __result && __service.{method.Name}({arguments});");
            }
            builder.AppendLine($"return __result;");
        }
        else
        {
            // CompositeResult.Any - logical OR
            builder.AppendLine($"var __result = false;");
            builder.AppendLine($"foreach (var __service in {helperAccessor})");
            using (builder.BeginScope())
            {
                builder.AppendLine($"__result = __result || __service.{method.Name}({arguments});");
            }
            builder.AppendLine($"return __result;");
        }
    }

    private static void AppendCompositeTaskSequentialMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor,
        string arguments
    )
    {
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"await __service.{method.Name}({arguments});");
        }
    }

    private static void AppendCompositeAggregateMethodBody(
        IndentedStringBuilder builder,
        string methodName,
        string helperAccessor,
        string arguments,
        string returnType,
        string aggregatorName
    )
    {
        builder.AppendLine($"var __hasResult = false;");
        builder.AppendLine($"var __result = default({returnType});");
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"var __current = __service.{methodName}({arguments});");
            builder.AppendLine("if (!__hasResult)");
            using (builder.BeginScope())
            {
                builder.AppendLine("__result = __current;");
                builder.AppendLine("__hasResult = true;");
            }
            builder.AppendLine("else");
            using (builder.BeginScope())
            {
                builder.AppendLine($"__result = {aggregatorName}(__result, __current);");
            }
        }
        builder.AppendLine("return __result;");
    }

    private static void AppendCompositeEnumerableMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor,
        string arguments,
        string returnType
    )
    {
        // For IEnumerable/ICollection/IList, concatenate all results
        var elementType = ExtractEnumerableType(returnType);
        builder.AppendLine(
            $"var __results = new global::System.Collections.Generic.List<{elementType}>();"
        );
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"var __serviceResult = __service.{method.Name}({arguments});");
            builder.AppendLine($"if (__serviceResult != null)");
            using (builder.BeginScope())
            {
                builder.AppendLine($"__results.AddRange(__serviceResult);");
            }
        }
        builder.AppendLine($"return __results;");
    }

    private static string ExtractEnumerableType(string enumerableType)
    {
        // Extract T from IEnumerable<T>, ICollection<T>, IList<T>, etc.
        var match = System.Text.RegularExpressions.Regex.Match(enumerableType, @"<([^<>]+)>$");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return "object";
    }

    private static void AppendCompositeUnsupportedMethod(
        IndentedStringBuilder builder,
        string methodName
    )
    {
        builder.AppendLine(
            $"throw new {NotSupportedException}(\"{methodName} is not supported in this composite.\");"
        );
    }

    private static void AppendCompositeVoidMethodBody(
        IndentedStringBuilder builder,
        string methodName,
        string helperAccessor,
        string arguments
    )
    {
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"__service.{methodName}({arguments});");
        }
    }

    private static void AppendCompositeBoolMethodBody(
        IndentedStringBuilder builder,
        string methodName,
        string helperAccessor,
        string arguments,
        bool useAnd
    )
    {
        if (useAnd)
        {
            builder.AppendLine("foreach (var __service in " + helperAccessor + ")");
            using (builder.BeginScope())
            {
                builder.AppendLine($"if (!__service.{methodName}({arguments})) return false;");
            }
            builder.AppendLine("return true;");
        }
        else
        {
            builder.AppendLine("foreach (var __service in " + helperAccessor + ")");
            using (builder.BeginScope())
            {
                builder.AppendLine($"if (__service.{methodName}({arguments})) return true;");
            }
            builder.AppendLine("return false;");
        }
    }

    private static void AppendCompositeTaskSequentialMethodBody(
        IndentedStringBuilder builder,
        string methodName,
        string helperAccessor,
        string arguments
    )
    {
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"await __service.{methodName}({arguments});");
        }
        // No return statement needed for async Task method
    }
}
