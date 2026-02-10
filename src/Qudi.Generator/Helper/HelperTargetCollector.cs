using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

internal static class HelperTargetCollector
{
    private const string QudiDecoratorAttribute = "Qudi.QudiDecoratorAttribute";
    private const string QudiStrategyAttribute = "Qudi.QudiStrategyAttribute";

    public static IncrementalValueProvider<ImmutableArray<HelperTarget>> CollectTargets(
        IncrementalGeneratorInitializationContext context
    )
    {
        var decoratorTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiDecoratorAttribute,
                static (node, _) => true,
                static (ctx, _) => CreateTargets(ctx, isDecorator: true, isStrategy: false)
            )
            .SelectMany(static (targets, _) => targets);

        var strategyTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiStrategyAttribute,
                static (node, _) => true,
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
        if (
            context.TargetSymbol is not INamedTypeSymbol typeSymbol
            || context.Attributes.Length == 0
        )
        {
            return ImmutableArray<HelperTarget>.Empty;
        }

        // TODO: There may be multiple attributes
        var attribute = context.Attributes[0];
        var asTypes = GetExplicitAsTypes(attribute);

        // typename is only class name without namespace, such as "MyService"
        // TODO: Support nested classes (classes within classes)
        var typeName = typeSymbol.Name;
        var typeNamespace = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();
        var isRecord = typeSymbol.IsRecord;
        var typeKind = typeSymbol.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            _ => "class",
        };
        var typeKeyword = $"{(isRecord ? "record " : "")}{typeKind}";

        IEnumerable<INamedTypeSymbol> interfaces =
            asTypes.Length > 0 ? asTypes : typeSymbol.AllInterfaces.OfType<INamedTypeSymbol>();

        var targets = new List<HelperTarget>();
        var iface = interfaces.FirstOrDefault(
            iface => iface.TypeKind == TypeKind.Interface
        );
        if(iface == null)
        {
            return ImmutableArray<HelperTarget>.Empty;
        }

        var interfaceName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var interfaceNamespace = iface.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : iface.ContainingNamespace.ToDisplayString();
        var interfaceHelperName = SanitizeIdentifier(
            iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        );
        var members = CollectInterfaceMembers(iface);
        var target = new HelperTarget
        {
            ImplementingTypeName = typeName,
            ImplementingTypeNamespace = typeNamespace,
            ImplementingTypeKeyword = typeKeyword,
            InterfaceName = interfaceName,
            InterfaceNamespace = interfaceNamespace,
            InterfaceHelperName = interfaceHelperName,
            HelperNamespaceSuffix = SanitizeIdentifier(interfaceName),
            Members = new EquatableArray<HelperMember>(members),
            IsDecorator = isDecorator,
            IsStrategy = isStrategy,
        };

        return targets.Append(target).ToImmutableArray();
    }

    private static ImmutableArray<HelperTarget> MergeTargets(ImmutableArray<HelperTarget> targets)
    {
        if (targets.IsDefaultOrEmpty)
        {
            return ImmutableArray<HelperTarget>.Empty;
        }

        var map = new Dictionary<string, HelperTarget>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (!map.TryGetValue(target.InterfaceName, out var existing))
            {
                map[target.InterfaceName] = target;
                continue;
            }

            map[target.InterfaceName] = existing with
            {
                IsDecorator = existing.IsDecorator || target.IsDecorator,
                IsStrategy = existing.IsStrategy || target.IsStrategy,
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

    private static IEnumerable<HelperMember> CollectInterfaceMembers(
        INamedTypeSymbol interfaceSymbol
    )
    {
        var members = new List<HelperMember>();
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
                    members.Add(CreateMethodMember(method));
                    continue;
                }

                if (member is IPropertySymbol property)
                {
                    members.Add(CreatePropertyMember(property));
                }
            }
        }

        return members;
    }

    private static HelperMember CreateMethodMember(IMethodSymbol method)
    {
        var parameters = method.Parameters.Select(CreateParameter).ToImmutableArray();
        return new HelperMember()
        {
            Kind = HelperMemberKind.Method,
            Name = method.Name,
            ReturnTypeName = method.ReturnType.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            Parameters = new EquatableArray<HelperParameter>(parameters),
            HasGetter = false,
            HasSetter = false,
            IsIndexer = false,
        };
    }

    private static HelperMember CreatePropertyMember(IPropertySymbol property)
    {
        var parameters = property.Parameters.Select(CreateParameter).ToImmutableArray();
        return new HelperMember()
        {
            Kind = HelperMemberKind.Property,
            Name = property.Name,
            ReturnTypeName = property.Type.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            Parameters = new EquatableArray<HelperParameter>(parameters),
            HasGetter = property.GetMethod is not null,
            HasSetter = property.SetMethod is not null,
            IsIndexer = property.IsIndexer,
        };
    }

    private static HelperParameter CreateParameter(IParameterSymbol parameter)
    {
        return new HelperParameter
        {
            TypeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Name = parameter.Name,
            RefKindPrefix = GetRefKindPrefix(parameter.RefKind),
            IsParams = parameter.IsParams,
        };
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
        var builder = new System.Text.StringBuilder();
        foreach (var ch in span)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }
}
