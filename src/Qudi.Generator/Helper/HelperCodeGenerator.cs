using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Qudi.Generator;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

internal static class HelperCodeGenerator
{
    private const string IEnumerable = "global::System.Collections.Generic.IEnumerable";

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
            // TODO: Separate generation for concrete and interface sides
            // Output to Qudi.Helper.Concrete.g.cs and Qudi.Helper.Abstract.g.cs
            foreach (var helper in interfaceTargets)
            {
                GenerateHelperForInterface(context, helper);
            }

            foreach (var target in implementingTargets)
            {
                GeneratePartialConstructor(context, target);
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
        SourceProductionContext context,
        HelperInterfaceTarget helper
    )
    {
        var namespaceName = helper.InterfaceNamespace;

        var builder = new IndentedStringBuilder();
        builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
        var isUseNamespace = !string.IsNullOrEmpty(namespaceName);
        builder.AppendLineIf(isUseNamespace, $"namespace {namespaceName}");
        using(builder.BeginScopeIf(isUseNamespace))
        {
            if (helper.IsDecorator)
            {
                GenerateDecoratorHelper(builder, helper);
                builder.AppendLine("");
            }

            if (helper.IsStrategy)
            {
                GenerateStrategyHelper(builder, helper);
            }
        }

        context.AddSource($"Qudi.Helper.{helper.HelperNamespaceSuffix}.g.cs", builder.ToString());
    }

    private static void GenerateDecoratorHelper(
        IndentedStringBuilder builder,
        HelperInterfaceTarget helper
    )
    {
        var interfaceName = helper.InterfaceName;
        var interfaceHelperName = helper.InterfaceHelperName;
        var members = helper.Members.ToImmutableArray();
        var helperName = BuildHelperClassName(interfaceHelperName, isDecorator: true);
        builder.AppendLine($"public abstract class {helperName} : {interfaceName}");
        builder.AppendLine("{");
        builder.AppendLine($"    protected readonly {interfaceName} _innerService;");
        builder.AppendLine("");
        builder.AppendLine($"    protected {helperName}({interfaceName} innerService)");
        builder.AppendLine("    {");
        builder.AppendLine("        _innerService = innerService;");
        builder.AppendLine("    }");
        builder.IncreaseIndent();

        foreach (var member in members)
        {
            if (member.Kind == HelperMemberKind.Method)
            {
                AppendDecoratorMethod(builder, member);
            }
            else if (member.Kind == HelperMemberKind.Property)
            {
                AppendDecoratorProperty(builder, member);
            }
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void GenerateStrategyHelper(
        IndentedStringBuilder builder,
        HelperInterfaceTarget helper
    )
    {
        var interfaceName = helper.InterfaceName;
        var interfaceHelperName = helper.InterfaceHelperName;
        var members = helper.Members.ToImmutableArray();

        var helperName = BuildHelperClassName(interfaceHelperName, isDecorator: false);
        builder.AppendLine($"public abstract class {helperName} : {interfaceName}");
        builder.AppendLine("{");
        builder.AppendLine(
            $"    protected readonly {IEnumerable}<{interfaceName}> _services;"
        );
        builder.AppendLine("");
        builder.AppendLine(
            $"    protected {helperName}({IEnumerable}<{interfaceName}> services)"
        );
        builder.AppendLine("    {");
        builder.AppendLine("        _services = services;");
        builder.AppendLine("    }");
        builder.AppendLine("");
        builder.AppendLine(
            $"    protected abstract global::Qudi.StrategyResult ShouldUseService({interfaceName} service);"
        );
        builder.IncreaseIndent();

        foreach (var member in members)
        {
            if (member.Kind == HelperMemberKind.Method)
            {
                AppendStrategyMethod(builder, member);
            }
            else if (member.Kind == HelperMemberKind.Property)
            {
                AppendStrategyProperty(builder, member);
            }
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendDecoratorMethod(IndentedStringBuilder builder, HelperMember method)
    {
        var returnType = method.ReturnTypeName;
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);
        var returnsVoid = returnType == "void";

        if (returnsVoid)
        {
            builder.AppendLine(
                $"public virtual void {method.Name}({parameters}) => _innerService.{method.Name}({arguments});"
            );
            return;
        }

        builder.AppendLine(
            $"public virtual {returnType} {method.Name}({parameters}) => _innerService.{method.Name}({arguments});"
        );
    }

    private static void AppendDecoratorProperty(
        IndentedStringBuilder builder,
        HelperMember property
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

        builder.AppendLine($"public virtual {typeName} {propertyName}{indexerSuffix}");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        if (property.HasGetter)
        {
            builder.AppendLine(
                property.IsIndexer
                    ? $"get => _innerService{accessSuffix};"
                    : $"get => _innerService.{propertyName}{accessSuffix};"
            );
        }

        if (property.HasSetter)
        {
            builder.AppendLine(
                property.IsIndexer
                    ? $"set => _innerService{accessSuffix} = value;"
                    : $"set => _innerService.{propertyName}{accessSuffix} = value;"
            );
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendStrategyMethod(IndentedStringBuilder builder, HelperMember method)
    {
        var returnType = method.ReturnTypeName;
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);
        var returnsVoid = returnType == "void";

        builder.AppendLine($"public virtual {returnType} {method.Name}({parameters})");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        var resultVar = returnsVoid ? string.Empty : "result";
        if (!returnsVoid)
        {
            builder.AppendLine($"{returnType} {resultVar} = default!;");
            builder.AppendLine("var hasResult = false;");
        }

        builder.AppendLine("foreach (var service in _services)");
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine($"var decision = ShouldUseService(service);");
        builder.AppendLine("if (decision.UseService)");
        builder.AppendLine("{");
        builder.IncreaseIndent();
        if (returnsVoid)
        {
            builder.AppendLine($"service.{method.Name}({arguments});");
        }
        else
        {
            builder.AppendLine($"{resultVar} = service.{method.Name}({arguments});");
            builder.AppendLine("hasResult = true;");
        }
        builder.DecreaseIndent();
        builder.AppendLine("}");
        builder.AppendLine("if (!decision.Continue)");
        builder.AppendLine("{");
        builder.IncreaseIndent();
        builder.AppendLine("break;");
        builder.DecreaseIndent();
        builder.AppendLine("}");
        builder.DecreaseIndent();
        builder.AppendLine("}");

        if (!returnsVoid)
        {
            builder.AppendLine("return hasResult ? result : default!;");
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendStrategyProperty(IndentedStringBuilder builder, HelperMember property)
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

        builder.AppendLine($"public virtual {typeName} {propertyName}{indexerSuffix}");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        if (property.HasGetter)
        {
            builder.AppendLine("get");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine($"{typeName} result = default!;");
            builder.AppendLine("var hasResult = false;");
            builder.AppendLine("foreach (var service in _services)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine("var decision = ShouldUseService(service);");
            builder.AppendLine("if (decision.UseService)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine(
                property.IsIndexer
                    ? $"result = service{accessSuffix};"
                    : $"result = service.{propertyName}{accessSuffix};"
            );
            builder.AppendLine("hasResult = true;");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.AppendLine("if (!decision.Continue)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine("break;");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.AppendLine("return hasResult ? result : default!;");
            builder.DecreaseIndent();
            builder.AppendLine("}");
        }

        if (property.HasSetter)
        {
            builder.AppendLine("set");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine("foreach (var service in _services)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine("var decision = ShouldUseService(service);");
            builder.AppendLine("if (decision.UseService)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine(
                property.IsIndexer
                    ? $"service{accessSuffix} = value;"
                    : $"service.{propertyName}{accessSuffix} = value;"
            );
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.AppendLine("if (!decision.Continue)");
            builder.AppendLine("{");
            builder.IncreaseIndent();
            builder.AppendLine("break;");
            builder.DecreaseIndent();
            builder.AppendLine("}");
            builder.DecreaseIndent();
            builder.AppendLine("}");
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

    private static string BuildHelperClassName(string interfaceHelperName, bool isDecorator)
    {
        return isDecorator
            ? $"DecoratorHelper_{interfaceHelperName}"
            : $"StrategyHelper_{interfaceHelperName}";
    }

    private static void GeneratePartialConstructor(
        SourceProductionContext context,
        HelperImplementingTarget target
    )
    {
        var helperName = BuildHelperClassName(target.InterfaceHelperName, target.IsDecorator);
        var helperTypeName = string.IsNullOrEmpty(target.InterfaceNamespace)
            ? helperName
            : $"global::{target.InterfaceNamespace}.{helperName}";
        var constructorParameters = BuildParameterList(target.ConstructorParameters);
        var fieldParameters = target.ConstructorParameters
            .ToImmutableArray()
            .Where(parameter => parameter.Name != target.BaseParameterName)
            .ToArray();

        var builder = new IndentedStringBuilder();
        builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
        var useNamespace = !string.IsNullOrEmpty(target.ImplementingTypeNamespace);
        builder.AppendLineIf(useNamespace, $"namespace {target.ImplementingTypeNamespace}");
        using (builder.BeginScopeIf(useNamespace))
        {
            builder.AppendLine(
                $"partial {target.ImplementingTypeKeyword} {target.ImplementingTypeName} : {helperTypeName}"
            );
            builder.AppendLine("{");
            builder.IncreaseIndent();

            foreach (var parameter in fieldParameters)
            {
                builder.AppendLine($"private readonly {parameter.TypeName} {parameter.Name};");
            }

            if (fieldParameters.Length > 0)
            {
                builder.AppendLine("");
            }

            builder.AppendLine(
                $"{target.ConstructorAccessibility} partial {target.ImplementingTypeName}({constructorParameters}) : base({target.BaseParameterName})"
            );
            builder.AppendLine("{");
            builder.IncreaseIndent();
            foreach (var parameter in fieldParameters)
            {
                builder.AppendLine($"this.{parameter.Name} = {parameter.Name};");
            }
            builder.DecreaseIndent();
            builder.AppendLine("}");

            builder.DecreaseIndent();
            builder.AppendLine("}");
        }

        var suffix = SanitizeIdentifier(
            $"{target.ImplementingTypeNamespace}_{target.ImplementingTypeName}_{helperName}"
        );
        context.AddSource($"Qudi.Helper.Constructor.{suffix}.g.cs", builder.ToString());
    }

    private static string SanitizeIdentifier(string text)
    {
        var span = text.Replace("global::", string.Empty);
        var builder = new StringBuilder();
        foreach (var ch in span)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }
}
