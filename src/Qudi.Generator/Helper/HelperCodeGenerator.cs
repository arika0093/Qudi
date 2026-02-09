using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Qudi.Generator;

namespace Qudi.Generator.Helper;

internal static class HelperCodeGenerator
{
    private const string HelperNamespace = "Qudi.Helper";

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
        var anyStrategy = helpers.Any(t => t.IsStrategy);

        if (anyStrategy)
        {
            GenerateStrategyResult(context);
        }

        foreach (var helper in helpers)
        {
            GenerateHelperForInterface(
                context,
                helper.InterfaceSymbol,
                helper.IsDecorator,
                helper.IsStrategy
            );
        }
    }

    private static void GenerateStrategyResult(SourceProductionContext context)
    {
        var builder = new IndentedStringBuilder();
        builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
        builder.AppendLine($"namespace {HelperNamespace}");
        builder.AppendLine("{");
        builder.AppendLine("    public readonly record struct StrategyResult");
        builder.AppendLine("    {");
        builder.AppendLine("        public bool UseService { get; init; }");
        builder.AppendLine("        public bool Continue { get; init; }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource("Qudi.Helper.StrategyResult.g.cs", builder.ToString());
    }

    private static void GenerateHelperForInterface(
        SourceProductionContext context,
        INamedTypeSymbol interfaceSymbol,
        bool generateDecorator,
        bool generateStrategy
    )
    {
        if (interfaceSymbol.TypeKind != TypeKind.Interface)
        {
            return;
        }

        var helperNameSuffix = SanitizeIdentifier(
            interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        );

        var interfaceName = interfaceSymbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var members = CollectInterfaceMembers(interfaceSymbol);

        var builder = new IndentedStringBuilder();
        builder.AppendLine(CodeTemplateContents.CommonGeneratedHeader);
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("");
        builder.AppendLine($"namespace {HelperNamespace}");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        if (generateDecorator)
        {
            GenerateDecoratorHelper(builder, interfaceName, helperNameSuffix, members);
            builder.AppendLine("");
        }

        if (generateStrategy)
        {
            GenerateStrategyHelper(builder, interfaceName, helperNameSuffix, members);
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");

        context.AddSource($"Qudi.Helper.{helperNameSuffix}.g.cs", builder.ToString());
    }

    private static ImmutableArray<ISymbol> CollectInterfaceMembers(INamedTypeSymbol interfaceSymbol)
    {
        var members = new List<ISymbol>();
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var iface in interfaceSymbol.AllInterfaces.Concat(new[] { interfaceSymbol }))
        {
            foreach (var member in iface.GetMembers())
            {
                if (!visited.Add(member))
                {
                    continue;
                }

                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    members.Add(member);
                    continue;
                }

                if (member is IPropertySymbol)
                {
                    members.Add(member);
                }
            }
        }

        return members.ToImmutableArray();
    }

    private static void GenerateDecoratorHelper(
        IndentedStringBuilder builder,
        string interfaceName,
        string helperNameSuffix,
        ImmutableArray<ISymbol> members
    )
    {
        builder.AppendLine(
            $"public abstract class DecoratorHelper_{helperNameSuffix} : {interfaceName}"
        );
        builder.AppendLine("{");
        builder.AppendLine($"    protected readonly {interfaceName} _innerService;");
        builder.AppendLine("");
        builder.AppendLine(
            $"    protected DecoratorHelper_{helperNameSuffix}({interfaceName} innerService)"
        );
        builder.AppendLine("    {");
        builder.AppendLine("        _innerService = innerService;");
        builder.AppendLine("    }");
        builder.IncreaseIndent();

        foreach (var member in members)
        {
            if (member is IMethodSymbol method)
            {
                AppendDecoratorMethod(builder, method);
            }
            else if (member is IPropertySymbol property)
            {
                AppendDecoratorProperty(builder, property);
            }
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void GenerateStrategyHelper(
        IndentedStringBuilder builder,
        string interfaceName,
        string helperNameSuffix,
        ImmutableArray<ISymbol> members
    )
    {
        builder.AppendLine(
            $"public abstract class StrategyHelper_{helperNameSuffix} : {interfaceName}"
        );
        builder.AppendLine("{");
        builder.AppendLine($"    protected readonly IEnumerable<{interfaceName}> _services;");
        builder.AppendLine("");
        builder.AppendLine(
            $"    protected StrategyHelper_{helperNameSuffix}(IEnumerable<{interfaceName}> services)"
        );
        builder.AppendLine("    {");
        builder.AppendLine("        _services = services;");
        builder.AppendLine("    }");
        builder.AppendLine("");
        builder.AppendLine(
            $"    protected abstract StrategyResult ShouldUseService({interfaceName} service);"
        );
        builder.IncreaseIndent();

        foreach (var member in members)
        {
            if (member is IMethodSymbol method)
            {
                AppendStrategyMethod(builder, method);
            }
            else if (member is IPropertySymbol property)
            {
                AppendStrategyProperty(builder, property);
            }
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendDecoratorMethod(IndentedStringBuilder builder, IMethodSymbol method)
    {
        var returnType = method.ReturnType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);

        if (method.ReturnsVoid)
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
        IPropertySymbol property
    )
    {
        var typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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

        if (property.GetMethod is not null)
        {
            builder.AppendLine(
                property.IsIndexer
                    ? $"get => _innerService{accessSuffix};"
                    : $"get => _innerService.{propertyName}{accessSuffix};"
            );
        }

        if (property.SetMethod is not null)
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

    private static void AppendStrategyMethod(IndentedStringBuilder builder, IMethodSymbol method)
    {
        var returnType = method.ReturnType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var parameters = BuildParameterList(method.Parameters);
        var arguments = BuildArgumentList(method.Parameters);

        builder.AppendLine($"public virtual {returnType} {method.Name}({parameters})");
        builder.AppendLine("{");
        builder.IncreaseIndent();

        var resultVar = method.ReturnsVoid ? string.Empty : "result";
        if (!method.ReturnsVoid)
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
        if (method.ReturnsVoid)
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

        if (!method.ReturnsVoid)
        {
            builder.AppendLine("return hasResult ? result : default!;");
        }

        builder.DecreaseIndent();
        builder.AppendLine("}");
    }

    private static void AppendStrategyProperty(
        IndentedStringBuilder builder,
        IPropertySymbol property
    )
    {
        var typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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

        if (property.GetMethod is not null)
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

        if (property.SetMethod is not null)
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

    private static string BuildParameterList(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0)
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

            builder.Append(GetRefKindPrefix(parameter.RefKind));
            builder.Append(
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            );
            builder.Append(' ');
            builder.Append(parameter.Name);
            return builder.ToString();
        });

        return string.Join(", ", parts);
    }

    private static string BuildArgumentList(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0)
        {
            return string.Empty;
        }

        var parts = parameters.Select(parameter =>
        {
            var prefix = GetRefKindPrefix(parameter.RefKind);
            return $"{prefix}{parameter.Name}";
        });

        return string.Join(", ", parts);
    }

    private static string GetRefKindPrefix(RefKind refKind)
    {
        return refKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty,
        };
    }

    private static string SanitizeIdentifier(string text)
    {
        var span = text.Replace("global::", string.Empty);
        var builder = new StringBuilder();
        foreach (var ch in span)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.ToString();
    }
}
