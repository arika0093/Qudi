using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Qudi.Generator;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

internal static class HelperCodeGenerator
{
    private const string IEnumerable = "global::System.Collections.IEnumerable";

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
        var helperName = BuildHelperInterfaceName(target.InterfaceHelperName);
        var helperTypeName = string.IsNullOrEmpty(target.InterfaceNamespace)
            ? helperName
            : $"global::{target.InterfaceNamespace}.{helperName}";
        // namespace
        var useNamespace = !string.IsNullOrEmpty(target.ImplementingTypeNamespace);
        builder.AppendLineIf(useNamespace, $"namespace {target.ImplementingTypeNamespace}");
        // class declaration
        using (builder.BeginScopeIf(useNamespace))
        {
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
        var helperName = BuildHelperInterfaceName(interfaceHelperName);
        var useIntercept = helper.UseIntercept;
        var helperAccessor = useIntercept ? "__Base" : "__Inner";

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
                    AppendDecoratorMethod(builder, member, helperAccessor);
                }
                else if (member.Kind == HelperMemberKind.Property)
                {
                    AppendDecoratorProperty(builder, member, helperAccessor);
                }
            }

            // helper accessor
            builder.AppendLine(
                $$"""
                {{interfaceName}} {{helperAccessor}} { get; }

                """
            );

            // base impl class and intercept method
            if (useIntercept)
            {
                builder.AppendLine(
                    $$"""
                    {{CodeTemplateContents.EditorBrowsableAttribute}}
                    {{IEnumerable}}<bool> Intercept(string methodName, object?[] args)
                    {
                        yield return true;
                    }

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
            {{asyncModifier}}{{returnType}} {{method.Name}}({{parameters}})
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

        builder.AppendLine($"public {typeName} {propertyName}{indexerSuffix}");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        if (property.HasGetter)
        {
            var getterAccess = property.IsIndexer
                ? $"__Service{accessSuffix}"
                : $"__Service.{propertyName}{accessSuffix}";
            builder.AppendLine("get");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine(
                $"using var enumerator = __Root.Intercept(\"get_{propertyName}\", new object?[] {{ {BuildInterceptArgumentList(property.Parameters)} }}).GetEnumerator();"
            );
            builder.AppendLine("if (enumerator.MoveNext() && enumerator.Current)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine($"var result = {getterAccess};");
            builder.AppendLine("enumerator.MoveNext();");
            builder.AppendLine("return result;");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.AppendLine(
                $"throw new global::System.InvalidOperationException(\"Execution of get_{propertyName} was cancelled by Intercept.\");"
            );
            builder.DecreaseIndent();
            builder.AppendLine("}");
        }

        if (property.HasSetter)
        {
            var setterAccess = property.IsIndexer
                ? $"__Service{accessSuffix}"
                : $"__Service.{propertyName}{accessSuffix}";
            builder.AppendLine("set");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            var setterArgs = BuildInterceptArgumentListWithValue(property.Parameters);
            builder.AppendLine(
                $"using var enumerator = __Root.Intercept(\"set_{propertyName}\", new object?[] {{ {setterArgs} }}).GetEnumerator();"
            );
            builder.AppendLine("if (enumerator.MoveNext() && enumerator.Current)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine($"{setterAccess} = value;");
            builder.AppendLine("enumerator.MoveNext();");
            builder.AppendLine("return;");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.AppendLine(
                $"throw new global::System.InvalidOperationException(\"Execution of set_{propertyName} was cancelled by Intercept.\");"
            );
            builder.DecreaseIndent();
            builder.AppendLine("}");
        }

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

    private static string BuildHelperInterfaceName(string interfaceHelperName)
    {
        return $"IDecoratorHelper_{interfaceHelperName}";
    }

    private static bool IsTaskLikeReturnType(string returnType)
    {
        return IsTaskLikeNonGenericReturnType(returnType)
            || returnType.StartsWith("global::System.Threading.Tasks.Task<")
            || returnType.StartsWith("global::System.Threading.Tasks.ValueTask<");
    }

    private static bool IsTaskLikeNonGenericReturnType(string returnType)
    {
        return returnType == "global::System.Threading.Tasks.Task"
            || returnType == "global::System.Threading.Tasks.ValueTask";
    }
}
