using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Qudi.Generator;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

internal static class HelperCodeGenerator
{
    public static void GenerateHelpers(SourceProductionContext context, HelperGenerationInput input)
    {
        var interfaceTargets = input.InterfaceTargets;
        var implementingTargets = input.ImplementingTargets;

        if (interfaceTargets.Count == 0 && implementingTargets.Count == 0)
        {
            return;
        }

        try
        {
            if (interfaceTargets.Count > 0)
            {
                var builder = new IndentedStringBuilder();
                builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
                foreach (var helper in interfaceTargets)
                {
                    GenerateHelperForInterface(builder, helper);
                    builder.AppendLine("");
                }

                context.AddSource("Qudi.Helper.Abstracts.g.cs", builder.ToString());
            }

            if (implementingTargets.Count > 0)
            {
                var builder = new IndentedStringBuilder();
                builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
                foreach (var target in implementingTargets)
                {
                    GeneratePartialConstructor(builder, target);
                    builder.AppendLine("");
                }

                context.AddSource("Qudi.Helper.Constructor.g.cs", builder.ToString());
            }
        }
        catch (System.Exception ex)
        {
            context.AddSource(
                "Qudi.Helper.GenerationError.g.cs",
                $"""
                /* {ex} */
                """
            );
        }
    }

    private static void GenerateHelperForInterface(
        IndentedStringBuilder builder,
        HelperInterfaceTarget helper
    )
    {
        var namespaceName = helper.InterfaceNamespace;
        var isUseNamespace = !string.IsNullOrEmpty(namespaceName);
        builder.AppendLineIf(isUseNamespace, $"namespace {namespaceName}");
        using (builder.BeginScopeIf(isUseNamespace))
        {
            GenerateDecoratorHelper(builder, helper);
        }
    }

    private static void GenerateDecoratorHelper(
        IndentedStringBuilder builder,
        HelperInterfaceTarget helper
    )
    {
        var interfaceName = helper.InterfaceName;
        var interfaceHelperName = helper.InterfaceHelperName;
        var members = helper.Members.ToImmutableArray();
        var helperName = BuildHelperInterfaceName(interfaceHelperName);
        builder.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine($"public interface {helperName} : {interfaceName}");
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine("global::System.Collections.Generic.IEnumerable<bool> Intercept(string methodName, object?[] args)");
        builder.AppendLine("{");
        builder.AppendLine("    yield return true;");
        builder.AppendLine("}");
        builder.AppendLine("");
        builder.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine("protected __BaseImpl __Base { get; }");
        builder.AppendLine("");

        foreach (var member in members)
        {
            if (member.Kind == HelperMemberKind.Method)
            {
                AppendDecoratorMethod(builder, member, interfaceName);
            }
            else if (member.Kind == HelperMemberKind.Property)
            {
                AppendDecoratorProperty(builder, member, interfaceName);
            }
        }
        builder.AppendLine("");
        builder.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        builder.AppendLine($"protected class __BaseImpl({interfaceName} __Service, {helperName} __Root)");
        builder.AppendLine("{");
        builder.IncreaseIndent();
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
        builder.DecreaseIndent();
        builder.AppendLine("}");
        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendDecoratorMethod(
        IndentedStringBuilder builder,
        HelperMember method,
        string interfaceName
    )
    {
        var returnType = method.ReturnTypeName;
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);
        builder.AppendLine($"{returnType} {interfaceName}.{method.Name}({parameters}) => __Base.{method.Name}({arguments});");
    }

    private static void AppendDecoratorProperty(
        IndentedStringBuilder builder,
        HelperMember property,
        string interfaceName
    )
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
        builder.AppendLine($"{typeName} {interfaceName}.{propertyName}{indexerSuffix}");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        if (property.HasGetter)
        {
            var getterAccess = property.IsIndexer
                ? $"__Base{accessSuffix}"
                : $"__Base.{propertyName}{accessSuffix}";
            builder.AppendLine($"get => {getterAccess};");
        }

        if (property.HasSetter)
        {
            var setterAccess = property.IsIndexer
                ? $"__Base{accessSuffix}"
                : $"__Base.{propertyName}{accessSuffix}";
            builder.AppendLine($"set => {setterAccess} = value;");
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendBaseImplMethod(IndentedStringBuilder builder, HelperMember method)
    {
        var returnType = method.ReturnTypeName;
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);
        var interceptArguments = BuildInterceptArgumentList(method.Parameters);
        var returnsVoid = returnType == "void";

        builder.AppendLine($"public {returnType} {method.Name}({parameters})");
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine(
            $"using var enumerator = __Root.Intercept(\"{method.Name}\", new object?[] {{ {interceptArguments} }}).GetEnumerator();"
        );
        builder.AppendLine("if (enumerator.MoveNext() && enumerator.Current)");
        builder.AppendLine("{");
        builder.IncreaseIndent();
        if (returnsVoid)
        {
            builder.AppendLine($"__Service.{method.Name}({arguments});");
            builder.AppendLine("enumerator.MoveNext();");
            builder.AppendLine("return;");
        }
        else
        {
            builder.AppendLine($"var result = __Service.{method.Name}({arguments});");
            builder.AppendLine("enumerator.MoveNext();");
            builder.AppendLine("return result;");
        }
        builder.DecreaseIndent();
        builder.AppendLine("}");
        builder.AppendLine(
            $"throw new global::System.InvalidOperationException(\"Execution of {method.Name} was cancelled by Intercept.\");"
        );
        builder.DecreaseIndent();
        builder.AppendLine("}");
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
            .Concat(new[] { "value" });

        return string.Join(", ", parts);
    }

    private static string BuildHelperInterfaceName(string interfaceHelperName)
    {
        return $"IDecoratorHelper_{interfaceHelperName}";
    }

    private static void GeneratePartialConstructor(
        IndentedStringBuilder builder,
        HelperImplementingTarget target
    )
    {
        var helperName = BuildHelperInterfaceName(target.InterfaceHelperName);
        var helperTypeName = string.IsNullOrEmpty(target.InterfaceNamespace)
            ? helperName
            : $"global::{target.InterfaceNamespace}.{helperName}";
        var useNamespace = !string.IsNullOrEmpty(target.ImplementingTypeNamespace);
        builder.AppendLineIf(useNamespace, $"namespace {target.ImplementingTypeNamespace}");
        using (builder.BeginScopeIf(useNamespace))
        {
            builder.AppendLine(
                $"partial {target.ImplementingTypeKeyword} {target.ImplementingTypeName} : {helperTypeName}"
            );
            builder.AppendLine("{");
            builder.IncreaseIndent();

            builder.AppendLine(
                $"private {helperTypeName}.__BaseImpl Base => __baseCache ??= new({target.BaseParameterName}, this);"
            );
            builder.AppendLine($"private {helperTypeName}.__BaseImpl? __baseCache;");
            builder.AppendLine(
                $"{helperTypeName}.__BaseImpl {helperTypeName}.__Base => Base;"
            );

            builder.DecreaseIndent();
            builder.AppendLine("}");
        }
    }
}
