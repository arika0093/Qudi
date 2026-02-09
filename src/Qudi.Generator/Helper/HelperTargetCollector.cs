using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Qudi.Generator.Helper;

internal static class HelperTargetCollector
{
    private const string QudiDecoratorAttribute = "Qudi.QudiDecoratorAttribute";
    private const string QudiStrategyAttribute = "Qudi.QudiStrategyAttribute";

    public static IncrementalValueProvider<ImmutableArray<HelperTarget>> CollectTargets(
        IncrementalGeneratorInitializationContext context
    )
    {
        var decoratorTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QudiDecoratorAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => CreateTargets(ctx, isDecorator: true, isStrategy: false)
            )
            .SelectMany(static (targets, _) => targets);

        var strategyTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QudiStrategyAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => CreateTargets(ctx, isDecorator: false, isStrategy: true)
            )
            .SelectMany(static (targets, _) => targets);

        var combined = decoratorTargets.Collect().Combine(strategyTargets.Collect());
        return combined.Select(
            static (targets, _) => MergeTargets(targets.Left.AddRange(targets.Right))
        );
    }

    private static ImmutableArray<HelperTarget> CreateTargets(
        GeneratorAttributeSyntaxContext context,
        bool isDecorator,
        bool isStrategy
    )
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || context.Attributes.Length == 0)
        {
            return ImmutableArray<HelperTarget>.Empty;
        }

        var attribute = context.Attributes[0];
        var asTypes = GetExplicitAsTypes(attribute);

        IEnumerable<INamedTypeSymbol> interfaces = asTypes.Length > 0
            ? asTypes
            : typeSymbol.AllInterfaces.OfType<INamedTypeSymbol>();

        var targets = new List<HelperTarget>();
        foreach (var iface in interfaces)
        {
            if (iface.TypeKind != TypeKind.Interface)
            {
                continue;
            }

            targets.Add(new HelperTarget(iface, isDecorator, isStrategy));
        }

        return targets.ToImmutableArray();
    }

    private static ImmutableArray<HelperTarget> MergeTargets(
        ImmutableArray<HelperTarget> targets
    )
    {
        if (targets.IsDefaultOrEmpty)
        {
            return ImmutableArray<HelperTarget>.Empty;
        }

        var map = new Dictionary<INamedTypeSymbol, HelperTarget>(SymbolEqualityComparer.Default);
        foreach (var target in targets)
        {
            if (!map.TryGetValue(target.InterfaceSymbol, out var existing))
            {
                map[target.InterfaceSymbol] = target;
                continue;
            }

            map[target.InterfaceSymbol] = existing with
            {
                IsDecorator = existing.IsDecorator || target.IsDecorator,
                IsStrategy = existing.IsStrategy || target.IsStrategy
            };
        }

        return map.Values.ToImmutableArray();
    }

    private static ImmutableArray<INamedTypeSymbol> GetExplicitAsTypes(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key != "AsTypes" || argument.Value.Values.Length == 0)
            {
                continue;
            }

            var list = new List<INamedTypeSymbol>();
            foreach (var value in argument.Value.Values)
            {
                if (value.Value is INamedTypeSymbol typeSymbol)
                {
                    list.Add(typeSymbol);
                }
            }

            return list.ToImmutableArray();
        }

        return ImmutableArray<INamedTypeSymbol>.Empty;
    }
}
