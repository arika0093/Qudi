using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Qudi.Generator;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

internal static class HelperCodeGenerator
{
    private const string IEnumerable = "global::System.Collections.Generic.IEnumerable";
    private const string Task = "global::System.Threading.Tasks.Task";
    private const string ValueTask = "global::System.Threading.Tasks.ValueTask";

    // Static compiled regex for better performance
    private static readonly Regex EnumerableTypeExtractor = new Regex(
        @"<([^<>]+)>$",
        RegexOptions.Compiled
    );

    public static void GenerateHelpers(SourceProductionContext context, HelperGenerationInput input)
    {
        var interfaceTargets = input.InterfaceTargets;
        var implementingTargets = input.ImplementingTargets;

        if (interfaceTargets.Count == 0 && implementingTargets.Count == 0)
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

        if (implementingTargets.Count > 0)
        {
            var builder = new IndentedStringBuilder();
            builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
            foreach (var target in implementingTargets)
            {
                GeneratePartialClass(builder, target);
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
        var helperName = BuildHelperInterfaceName(target.InterfaceHelperName, target.IsComposite);
        var helperTypeName = string.IsNullOrEmpty(target.InterfaceNamespace)
            ? helperName
            : $"global::{target.InterfaceNamespace}.{helperName}";
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
                    partial {{target.ImplementingTypeKeyword}} {{target.ImplementingTypeName}} : {{helperTypeName}}
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
                    $$"""
                    partial {{target.ImplementingTypeKeyword}} {{target.ImplementingTypeName}} : {{helperTypeName}}
                    {
                        {{CodeTemplateContents.EditorBrowsableAttribute}}
                        global::System.Collections.Generic.IEnumerable<{{target.InterfaceName}}> {{helperTypeName}}.__InnerServices => {{target.BaseParameterName}};
                    }
                    """
                );
            }
            else
            {
                builder.AppendLine(
                    $$"""
                    partial {{target.ImplementingTypeKeyword}} {{target.ImplementingTypeName}} : {{helperTypeName}}
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
        var interfaceHelperName = helper.InterfaceHelperName;
        var members = helper.Members.ToImmutableArray();
        var useIntercept = helper.UseIntercept;
        var isComposite = helper.IsComposite;
        var helperAccessor = useIntercept ? "__Base" : "__Inner";
        if (!useIntercept && isComposite)
        {
            helperAccessor = "__InnerServices";
        }
        var helperName = BuildHelperInterfaceName(interfaceHelperName, isComposite);

        // Generate interface
        builder.AppendLine(
            $$"""
            {{CodeTemplateContents.EmbeddedAttributeUsage}}
            public interface {{helperName}} : {{interfaceName}}
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
                        AppendCompositeMethod(builder, member, helperAccessor);
                    }
                    else
                    {
                        AppendDecoratorMethod(builder, member, helperAccessor);
                    }
                }
                else if (member.Kind == HelperMemberKind.Property)
                {
                    if (isComposite)
                    {
                        AppendCompositeProperty(builder, member, helperAccessor);
                    }
                    else
                    {
                        AppendDecoratorProperty(builder, member, helperAccessor);
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
                            AppendBaseImplMethod(builder, member);
                        }
                        else if (member.Kind == HelperMemberKind.Property)
                        {
                            AppendBaseImplProperty(builder, member);
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

    private static void AppendDecoratorMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor
    )
    {
        var returnType = method.ReturnTypeName;
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);
        var interfaceName = method.DeclaringInterfaceName;
        builder.AppendLine(
            $"{returnType} {interfaceName}.{method.Name}({parameters}) => {helperAccessor}.{method.Name}({arguments});"
        );
    }

    private static void AppendDecoratorProperty(
        IndentedStringBuilder builder,
        HelperMember property,
        string helperAccessor
    )
    {
        var typeName = property.ReturnTypeName;
        var propertyName = property.IsIndexer ? "this" : property.Name;
        var parameters = property.IsIndexer ? BuildParameterList(property.Parameters) : "";
        var indexerSuffix = property.IsIndexer ? $"[{parameters}]" : "";
        var accessSuffix = property.IsIndexer ? $"[{BuildArgumentList(property.Parameters)}]" : "";
        var getterAccess = property.IsIndexer
            ? $"{helperAccessor}{accessSuffix}"
            : $"{helperAccessor}.{propertyName}{accessSuffix}";
        var setterAccess = property.IsIndexer
            ? $"{helperAccessor}{accessSuffix}"
            : $"{helperAccessor}.{propertyName}{accessSuffix}";
        var interfaceName = property.DeclaringInterfaceName;

        builder.AppendLine($"{typeName} {interfaceName}.{propertyName}{indexerSuffix}");
        using (builder.BeginScope())
        {
            builder.AppendLineIf(property.HasGetter, $"get => {getterAccess};");
            builder.AppendLineIf(property.HasSetter, $"set => {setterAccess} = value;");
        }
    }

    private static void AppendCompositeMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor
    )
    {
        var returnType = method.ReturnTypeName;
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);
        var interfaceName = method.DeclaringInterfaceName;

        // Determine return type category
        // Note: Using string-based detection for simplicity. This works for most common cases
        // but may need enhancement for edge cases with similar type names.
        var isVoid = returnType == "void";
        var isBool = returnType == "bool";
        var isTask = returnType.StartsWith(Task);
        var isIEnumerable =
            returnType.Contains("IEnumerable")
            || returnType.Contains("ICollection")
            || returnType.Contains("IList");

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
                    returnType,
                    useWhenAll: true
                );
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
                AppendCompositeDefaultMethod(
                    builder,
                    method,
                    helperAccessor,
                    arguments,
                    returnType
                );
            }
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
        string returnType,
        bool useWhenAll
    )
    {
        // For Task/Task<T>, collect all tasks
        builder.AppendLine(
            $"var __tasks = new global::System.Collections.Generic.List<{returnType}>();"
        );
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
            if (returnType == Task)
            {
                builder.AppendLine($"return {Task}.WhenAny(__tasks);");
            }
            else
            {
                // Task<T> - WhenAny returns Task<Task<T>>, so we need to unwrap
                builder.AppendLine($"var __firstCompleted = await {Task}.WhenAny(__tasks);");
                builder.AppendLine($"return await __firstCompleted;");
            }
        }
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
        builder.AppendLine(
            $"var __results = new global::System.Collections.Generic.List<{ExtractEnumerableType(returnType)}>();"
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

    private static void AppendCompositeDefaultMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor,
        string arguments,
        string returnType
    )
    {
        // For other types, return the first non-null/non-default result
        builder.AppendLine($"foreach (var __service in {helperAccessor})");
        using (builder.BeginScope())
        {
            builder.AppendLine($"var __result = __service.{method.Name}({arguments});");
            builder.AppendLine(
                $"if (!global::System.Collections.Generic.EqualityComparer<{returnType}>.Default.Equals(__result, default!))"
            );
            using (builder.BeginScope())
            {
                builder.AppendLine($"return __result;");
            }
        }
        builder.AppendLine($"return default!;");
    }

    private static string ExtractEnumerableType(string enumerableType)
    {
        // Extract T from IEnumerable<T>, ICollection<T>, IList<T>, etc.
        // Use the static compiled regex for better performance
        var match = EnumerableTypeExtractor.Match(enumerableType);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return "object";
    }

    private static void AppendCompositeProperty(
        IndentedStringBuilder builder,
        HelperMember property,
        string helperAccessor
    )
    {
        var typeName = property.ReturnTypeName;
        var propertyName = property.IsIndexer ? "this" : property.Name;
        var parameters = property.IsIndexer ? BuildParameterList(property.Parameters) : "";
        var indexerSuffix = property.IsIndexer ? $"[{parameters}]" : "";
        var arguments = property.IsIndexer ? BuildArgumentList(property.Parameters) : "";
        var interfaceName = property.DeclaringInterfaceName;

        builder.AppendLine($"{typeName} {interfaceName}.{propertyName}{indexerSuffix}");
        using (builder.BeginScope())
        {
            // For composite properties, we just return the first service's property value by default
            if (property.HasGetter)
            {
                var access = property.IsIndexer
                    ? $"{helperAccessor}.FirstOrDefault()?[{arguments}]"
                    : $"{helperAccessor}.FirstOrDefault()?.{propertyName}";
                builder.AppendLine($"get => {access} ?? default!;");
            }
            if (property.HasSetter)
            {
                builder.AppendLine("set");
                using (builder.BeginScope())
                {
                    builder.AppendLine($"foreach (var __service in {helperAccessor})");
                    using (builder.BeginScope())
                    {
                        var access = property.IsIndexer
                            ? $"__service[{arguments}]"
                            : $"__service.{propertyName}";
                        builder.AppendLine($"{access} = value;");
                    }
                }
            }
        }
    }

    private static void AppendBaseImplMethod(IndentedStringBuilder builder, HelperMember method)
    {
        var returnType = method.ReturnTypeName;
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);
        var interceptArguments = BuildInterceptArgumentList(method.Parameters);
        var returnsVoid = returnType == "void";
        var isTaskLike = IsTaskLikeReturnType(returnType);
        var isTaskLikeNonGeneric = IsTaskLikeNonGenericReturnType(returnType);
        var useResult = !returnsVoid && !isTaskLikeNonGeneric;
        var resultVarSyntax = useResult ? "var result = " : "";
        var resultReturnSyntax = useResult ? "return result;" : "return;";
        var asyncModifier = isTaskLike ? "async " : "";
        var awaitModifier = isTaskLike ? "await " : "";

        builder.AppendLine(
            $$"""
            public {{asyncModifier}}{{returnType}} {{method.Name}}({{parameters}})
            {
                using var enumerator = __Root.Intercept("{{method.Name}}", new object?[] { {{interceptArguments}} }).GetEnumerator();
                if (enumerator.MoveNext() && enumerator.Current)
                {
                    {{resultVarSyntax}}{{awaitModifier}}__Service.{{method.Name}}({{arguments}});
                    enumerator.MoveNext();
                    {{resultReturnSyntax}}
                }
                throw new global::System.InvalidOperationException("Execution of {{method.Name}} was cancelled by Intercept.");
            }
            """
        );
    }

    private static void AppendBaseImplProperty(IndentedStringBuilder builder, HelperMember property)
    {
        var typeName = property.ReturnTypeName;
        var propertyName = property.IsIndexer ? "this" : property.Name;
        var parameters = property.IsIndexer
            ? BuildParameterList(property.Parameters)
            : string.Empty;
        var indexerSuffix = property.IsIndexer ? $"[{parameters}]" : string.Empty;
        var accessSuffix = property.IsIndexer
            ? $"[{BuildArgumentList(property.Parameters)}]"
            : string.Empty;
        var accessor = property.IsIndexer
            ? $"__Service{accessSuffix}"
            : $"__Service.{propertyName}{accessSuffix}";

        builder.AppendLine($"public {typeName} {propertyName}{indexerSuffix}");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        builder.AppendLineIf(
            property.HasGetter,
            $$"""
            get
            {
                using var enumerator = __Root.Intercept("get_{{propertyName}}", new object?[] { {{BuildInterceptArgumentList(
                property.Parameters
            )}} }).GetEnumerator();
                if (enumerator.MoveNext() && enumerator.Current)
                {
                    var result = {{accessor}};
                    enumerator.MoveNext();
                    return result;
                }
                throw new global::System.InvalidOperationException("Execution of get_{{propertyName}} was cancelled by Intercept.");
            }
            """
        );
        builder.AppendLineIf(
            property.HasSetter,
            $$"""
            set
            {
                using var enumerator = __Root.Intercept("set_{{propertyName}}", new object?[] { {{BuildInterceptArgumentListWithValue(
                property.Parameters
            )}} }).GetEnumerator();
                if (enumerator.MoveNext() && enumerator.Current)
                {
                    {{accessor}} = value;
                    enumerator.MoveNext();
                    return;
                }
                throw new global::System.InvalidOperationException("Execution of set_{{propertyName}} was cancelled by Intercept.");
            }
            """
        );
        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static string BuildParameterList(EquatableArray<HelperParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(parameter =>
        {
            var builder = new StringBuilder();
            if (parameter.IsParams)
            {
                builder.Append("params ");
            }

            builder.Append(parameter.RefKindPrefix);
            builder.Append(parameter.TypeName);
            builder.Append(' ');
            builder.Append(parameter.Name);
            return builder.ToString();
        });

        return string.Join(", ", parts);
    }

    private static string BuildArgumentList(EquatableArray<HelperParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(parameter => $"{parameter.RefKindPrefix}{parameter.Name}");

        return string.Join(", ", parts);
    }

    private static string BuildInterceptArgumentList(EquatableArray<HelperParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(parameter =>
            parameter.RefKindPrefix == "out " ? "null" : parameter.Name
        );

        return string.Join(", ", parts);
    }

    private static string BuildInterceptArgumentListWithValue(
        EquatableArray<HelperParameter> parameters
    )
    {
        if (parameters.Count == 0)
        {
            return "value";
        }

        var parts = parameters
            .Select(parameter => parameter.RefKindPrefix == "out " ? "null" : parameter.Name)
            .Concat(["value"]);

        return string.Join(", ", parts);
    }

    private static string BuildHelperInterfaceName(string interfaceHelperName, bool isComposite)
    {
        return isComposite
            ? $"ICompositeHelper_{interfaceHelperName}"
            : $"IDecoratorHelper_{interfaceHelperName}";
    }

    private static bool IsTaskLikeReturnType(string returnType)
    {
        return IsTaskLikeNonGenericReturnType(returnType)
            || returnType.StartsWith($"{Task}<")
            || returnType.StartsWith($"{ValueTask}<");
    }

    private static bool IsTaskLikeNonGenericReturnType(string returnType)
    {
        return returnType == $"{Task}" || returnType == $"{ValueTask}";
    }
}
