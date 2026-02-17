using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

/// <summary>
/// Generates helper code for decorator pattern implementations.
/// </summary>
internal static class DecoratorCodeGenerator
{
    public static void AppendDecoratorMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string helperAccessor
    )
    {
        var returnType = method.ReturnTypeName;
        var parameters = HelperCodeGeneratorUtility.BuildParameterList(method.Parameters);
        var arguments = HelperCodeGeneratorUtility.BuildArgumentList(method.Parameters);
        var interfaceName = method.DeclaringInterfaceName;
        builder.AppendLine(
            $"{returnType} {interfaceName}.{method.Name}({parameters}) => {helperAccessor}.{method.Name}({arguments});"
        );
    }

    public static void AppendDecoratorProperty(
        IndentedStringBuilder builder,
        HelperMember property,
        string helperAccessor
    )
    {
        var typeName = property.ReturnTypeName;
        var propertyName = property.IsIndexer ? "this" : property.Name;
        var parameters = property.IsIndexer
            ? HelperCodeGeneratorUtility.BuildParameterList(property.Parameters)
            : "";
        var indexerSuffix = property.IsIndexer ? $"[{parameters}]" : "";
        var accessSuffix = property.IsIndexer
            ? $"[{HelperCodeGeneratorUtility.BuildArgumentList(property.Parameters)}]"
            : "";
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

    public static void AppendBaseImplMethod(IndentedStringBuilder builder, HelperMember method)
    {
        var returnType = method.ReturnTypeName;
        var parameters = HelperCodeGeneratorUtility.BuildParameterList(method.Parameters);
        var arguments = HelperCodeGeneratorUtility.BuildArgumentList(method.Parameters);
        var interceptArguments = HelperCodeGeneratorUtility.BuildInterceptArgumentList(
            method.Parameters
        );
        var returnsVoid = returnType == "void";
        var isTaskLike = HelperCodeGeneratorUtility.IsTaskLikeReturnType(returnType);
        var isTaskLikeNonGeneric = HelperCodeGeneratorUtility.IsTaskLikeNonGenericReturnType(
            returnType
        );
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

    public static void AppendBaseImplProperty(IndentedStringBuilder builder, HelperMember property)
    {
        var typeName = property.ReturnTypeName;
        var propertyName = property.IsIndexer ? "this" : property.Name;
        var parameters = property.IsIndexer
            ? HelperCodeGeneratorUtility.BuildParameterList(property.Parameters)
            : string.Empty;
        var indexerSuffix = property.IsIndexer ? $"[{parameters}]" : string.Empty;
        var accessSuffix = property.IsIndexer
            ? $"[{HelperCodeGeneratorUtility.BuildArgumentList(property.Parameters)}]"
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
                using var enumerator = __Root.Intercept("get_{{propertyName}}", new object?[] { {{HelperCodeGeneratorUtility.BuildInterceptArgumentList(
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
                using var enumerator = __Root.Intercept("set_{{propertyName}}", new object?[] { {{HelperCodeGeneratorUtility.BuildInterceptArgumentListWithValue(
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
}
