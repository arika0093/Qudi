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

        // For composite, we iterate over all inner services and call the method on each
        builder.AppendLine($"{returnType} {interfaceName}.{method.Name}({parameters})");
        using (builder.BeginScope())
        {
            if (isVoid)
            {
                AppendCompositeVoidMethod(builder, method, helperAccessor, arguments);
            }
            else if (isBool)
            {
                // Default to CompositeResult.All (logical AND) for bool
                // TODO: Support CompositeMethod attribute to override this
                AppendCompositeBoolMethod(builder, method, helperAccessor, arguments, useAnd: true);
            }
            else if (isTask)
            {
                // Default to Task.WhenAll
                // TODO: Support CompositeMethod attribute to override this with WhenAny
                AppendCompositeTaskMethod(
                    builder,
                    method,
                    helperAccessor,
                    arguments,
                    useWhenAll: true
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
        var parameters = property.IsIndexer ? HelperCodeGeneratorUtility.BuildParameterList(property.Parameters) : "";
        var indexerSuffix = property.IsIndexer ? $"[{parameters}]" : "";
        var interfaceName = property.DeclaringInterfaceName;

        builder.AppendLine($"{typeName} {interfaceName}.{propertyName}{indexerSuffix}");
        using (builder.BeginScope())
        {
            if (property.HasGetter)
            {
                builder.AppendLine($"get => throw new {NotSupportedException}(\"{propertyName} is not supported in this composite.\");");
            }
            if (property.HasSetter)
            {
                builder.AppendLine($"set => throw new {NotSupportedException}(\"{propertyName} is not supported in this composite.\");");
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

        builder.AppendLine($"public partial {returnType} {method.Name}({parameters})");
        using (builder.BeginScope())
        {
            if (isVoid)
            {
                AppendCompositeVoidMethodBody(builder, method.Name, innerServicesAccessor, arguments);
                return;
            }

            if (isBool)
            {
                var useAnd = method.ResultBehavior != CompositeResultBehavior.Any;
                AppendCompositeBoolMethodBody(builder, method.Name, innerServicesAccessor, arguments, useAnd);
                return;
            }

            if (isTask)
            {
                var useWhenAll = method.ResultBehavior != CompositeResultBehavior.Any;
                AppendCompositeTaskMethodBody(builder, method.Name, innerServicesAccessor, arguments, useWhenAll);
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

    private static void AppendCompositeTaskMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor,
        string arguments,
        bool useWhenAll
    )
    {
        builder.AppendLine($"var __tasks = new global::System.Collections.Generic.List<{Task}>();");
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"__tasks.Add(__service.{method.Name}({arguments}));");
        }

        if (useWhenAll)
        {
            // CompositeResult.All or default - use WhenAll (works for Task and Task<T>)
            builder.AppendLine($"return {Task}.WhenAll(__tasks);");
        }
        else
        {
            // CompositeResult.Any - use WhenAny
            builder.AppendLine($"return {Task}.WhenAny(__tasks);");
        }
    }

    private static void AppendCompositeUnsupportedMethod(IndentedStringBuilder builder, string methodName)
    {
        builder.AppendLine($"throw new {NotSupportedException}(\"{methodName} is not supported in this composite.\");");
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

    private static void AppendCompositeTaskMethodBody(
        IndentedStringBuilder builder,
        string methodName,
        string helperAccessor,
        string arguments,
        bool useWhenAll
    )
    {
        builder.AppendLine($"var __tasks = new global::System.Collections.Generic.List<{Task}>();");
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"__tasks.Add(__service.{methodName}({arguments}));");
        }

        builder.AppendLine(useWhenAll ? $"return {Task}.WhenAll(__tasks);" : $"return {Task}.WhenAny(__tasks);");
    }
}
