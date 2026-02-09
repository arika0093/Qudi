using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Qudi.Generator;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

internal static class HelperCodeGenerator
{
    public static void GenerateHelpers(
        SourceProductionContext context,
        ImmutableArray<HelperTarget> targets
    )
    {
        if (targets.IsDefaultOrEmpty)
        {
            return;
        }

        var helpers = targets.ToImmutableArray();
        foreach (var helper in helpers)
        {
            GenerateHelperForInterface(context, helper);
        }
    }

    private static void GenerateHelperForInterface(
        SourceProductionContext context,
        HelperTarget helper
    )
    {
        var interfaceName = helper.InterfaceName;
        var members = helper.Members.ToImmutableArray();
        var namespaceName = $"Qudi.Helper.{helper.HelperNamespaceSuffix}";

        var builder = new IndentedStringBuilder();
        builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using Qudi;");
        builder.AppendLine($"namespace {namespaceName};");
        builder.AppendLine("");

        if (helper.IsDecorator)
        {
            GenerateDecoratorHelper(builder, interfaceName, members);
            builder.AppendLine("");
        }

        if (helper.IsStrategy)
        {
            GenerateStrategyHelper(builder, interfaceName, members);
        }

        context.AddSource($"Qudi.Helper.{helper.HelperNamespaceSuffix}.g.cs", builder.ToString());
    }

    private static void GenerateDecoratorHelper(
        IndentedStringBuilder builder,
        string interfaceName,
        ImmutableArray<HelperMember> members
    )
    {
        builder.AppendLine($"public abstract class DecoratorHelper<T> : {interfaceName}");
        builder.AppendLine($"    where T : {interfaceName}");
        builder.AppendLine("{");
        builder.AppendLine("    protected readonly T _innerService;");
        builder.AppendLine("");
        builder.AppendLine("    protected DecoratorHelper(T innerService)");
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
        string interfaceName,
        ImmutableArray<HelperMember> members
    )
    {
        builder.AppendLine($"public abstract class StrategyHelper<T> : {interfaceName}");
        builder.AppendLine($"    where T : {interfaceName}");
        builder.AppendLine("{");
        builder.AppendLine("    protected readonly IEnumerable<T> _services;");
        builder.AppendLine("");
        builder.AppendLine("    protected StrategyHelper(IEnumerable<T> services)");
        builder.AppendLine("    {");
        builder.AppendLine("        _services = services;");
        builder.AppendLine("    }");
        builder.AppendLine("");
        builder.AppendLine(
            "    protected abstract global::Qudi.StrategyResult ShouldUseService(T service);"
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
}
