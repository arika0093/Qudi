using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Helper;

internal static class HelperTargetCollector
{
    private const string QudiDecoratorAttribute = "Qudi.QudiDecoratorAttribute";
    private const string QudiStrategyAttribute = "Qudi.QudiStrategyAttribute";

    public static IncrementalValueProvider<HelperGenerationInput> CollectTargets(
        IncrementalGeneratorInitializationContext context
    )
    {
        var decoratorTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiDecoratorAttribute,
                static (node, _) => true,
                static (ctx, _) => CreateTargets(ctx, isDecorator: true, isStrategy: false)
            )
            .Select(static (targets, _) => targets);

        var strategyTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiStrategyAttribute,
                static (node, _) => true,
                static (ctx, _) => CreateTargets(ctx, isDecorator: false, isStrategy: true)
            )
            .Select(static (targets, _) => targets);

        var combined = decoratorTargets.Collect().Combine(strategyTargets.Collect());
        return combined.Select(static (targets, _) => MergeTargets(targets.Left, targets.Right));
    }

    private static HelperGenerationInput CreateTargets(
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
            return new HelperGenerationInput
            {
                InterfaceTargets = new EquatableArray<HelperInterfaceTarget>(
                    ImmutableArray<HelperInterfaceTarget>.Empty
                ),
                ImplementingTargets = new EquatableArray<HelperImplementingTarget>(
                    ImmutableArray<HelperImplementingTarget>.Empty
                ),
            };
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

        var interfaceTargets = new List<HelperInterfaceTarget>();
        var implementingTargets = new List<HelperImplementingTarget>();
        var iface = interfaces.FirstOrDefault(iface => iface.TypeKind == TypeKind.Interface);
        if (iface is null)
        {
            return new HelperGenerationInput
            {
                InterfaceTargets = new EquatableArray<HelperInterfaceTarget>(
                    ImmutableArray<HelperInterfaceTarget>.Empty
                ),
                ImplementingTargets = new EquatableArray<HelperImplementingTarget>(
                    ImmutableArray<HelperImplementingTarget>.Empty
                ),
            };
        }

        var interfaceName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var interfaceNamespace = iface.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : iface.ContainingNamespace.ToDisplayString();
        var interfaceHelperName = SanitizeIdentifier(
            iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        );
        var members = CollectInterfaceMembers(iface);
        var target = new HelperInterfaceTarget
        {
            InterfaceName = interfaceName,
            InterfaceNamespace = interfaceNamespace,
            InterfaceHelperName = interfaceHelperName,
            HelperNamespaceSuffix = SanitizeIdentifier(interfaceName),
            Members = new EquatableArray<HelperMember>(members),
            IsDecorator = isDecorator,
            IsStrategy = isStrategy,
        };
        interfaceTargets.Add(target);

        var constructorTarget = FindPartialConstructorTarget(
            context,
            typeName,
            typeNamespace,
            typeKeyword,
            interfaceNamespace,
            interfaceHelperName,
            iface,
            isDecorator,
            isStrategy
        );
        if (constructorTarget is not null)
        {
            implementingTargets.Add(constructorTarget);
        }

        return new HelperGenerationInput
        {
            InterfaceTargets = new EquatableArray<HelperInterfaceTarget>(
                interfaceTargets.ToImmutableArray()
            ),
            ImplementingTargets = new EquatableArray<HelperImplementingTarget>(
                implementingTargets.ToImmutableArray()
            ),
        };
    }

    private static HelperGenerationInput MergeTargets(
        ImmutableArray<HelperGenerationInput> left,
        ImmutableArray<HelperGenerationInput> right
    )
    {
        var interfaceTargets = left
            .SelectMany(t => t.InterfaceTargets)
            .Concat(right.SelectMany(t => t.InterfaceTargets))
            .ToImmutableArray();

        var implementingTargets = left
            .SelectMany(t => t.ImplementingTargets)
            .Concat(right.SelectMany(t => t.ImplementingTargets))
            .ToImmutableArray();

        var mergedInterfaces = MergeInterfaceTargets(interfaceTargets);
        var mergedImplementing = MergeImplementingTargets(implementingTargets);

        return new HelperGenerationInput
        {
            InterfaceTargets = new EquatableArray<HelperInterfaceTarget>(mergedInterfaces),
            ImplementingTargets = new EquatableArray<HelperImplementingTarget>(mergedImplementing),
        };
    }

    private static ImmutableArray<HelperInterfaceTarget> MergeInterfaceTargets(
        ImmutableArray<HelperInterfaceTarget> targets
    )
    {
        if (targets.IsDefaultOrEmpty)
        {
            return ImmutableArray<HelperInterfaceTarget>.Empty;
        }

        var map = new Dictionary<string, HelperInterfaceTarget>(StringComparer.Ordinal);
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

    private static ImmutableArray<HelperImplementingTarget> MergeImplementingTargets(
        ImmutableArray<HelperImplementingTarget> targets
    )
    {
        if (targets.IsDefaultOrEmpty)
        {
            return ImmutableArray<HelperImplementingTarget>.Empty;
        }

        var map = new Dictionary<string, HelperImplementingTarget>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            var key = target.ImplementingTypeNamespace + "." + target.ImplementingTypeName;
            if (!map.TryGetValue(key, out var existing))
            {
                map[key] = target;
                continue;
            }

            map[key] = existing with
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

    private static HelperImplementingTarget? FindPartialConstructorTarget(
        GeneratorAttributeSyntaxContext context,
        string typeName,
        string typeNamespace,
        string typeKeyword,
        string interfaceNamespace,
        string interfaceHelperName,
        INamedTypeSymbol interfaceSymbol,
        bool isDecorator,
        bool isStrategy
    )
    {
        if (context.TargetNode is not ClassDeclarationSyntax classSyntax)
        {
            return null;
        }

        var partialConstructor = classSyntax
            .Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(constructor =>
                constructor.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword))
            );
        if (partialConstructor is null)
        {
            return null;
        }

        var ctorSymbol = context.SemanticModel.GetDeclaredSymbol(partialConstructor);
        if (ctorSymbol is null)
        {
            return null;
        }

        var baseParameter = FindBaseParameter(ctorSymbol.Parameters, interfaceSymbol, isDecorator);
        if (baseParameter is null)
        {
            return null;
        }

        var constructorParameters = ctorSymbol.Parameters.Select(parameter => new HelperParameter
        {
            TypeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Name = parameter.Name,
            RefKindPrefix = GetRefKindPrefix(parameter.RefKind),
            IsParams = parameter.IsParams,
        });

        return new HelperImplementingTarget
        {
            ImplementingTypeName = typeName,
            ImplementingTypeNamespace = typeNamespace,
            ImplementingTypeKeyword = typeKeyword,
            ConstructorAccessibility = GetAccessibility(ctorSymbol.DeclaredAccessibility),
            InterfaceNamespace = interfaceNamespace,
            InterfaceHelperName = interfaceHelperName,
            ConstructorParameters = new EquatableArray<HelperParameter>(constructorParameters),
            BaseParameterName = baseParameter.Name,
            IsDecorator = isDecorator,
            IsStrategy = isStrategy,
        };
    }

    private static IParameterSymbol? FindBaseParameter(
        ImmutableArray<IParameterSymbol> parameters,
        INamedTypeSymbol interfaceSymbol,
        bool isDecorator
    )
    {
        foreach (var parameter in parameters)
        {
            if (isDecorator)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, interfaceSymbol))
                {
                    return parameter;
                }

                continue;
            }

            if (ImplementsIEnumerableOf(parameter.Type, interfaceSymbol))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool ImplementsIEnumerableOf(ITypeSymbol typeSymbol, INamedTypeSymbol elementType)
    {
        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        foreach (var iface in namedType.AllInterfaces.Concat(new[] { namedType }))
        {
            if (iface is not INamedTypeSymbol ifaceNamed)
            {
                continue;
            }

            if (
                ifaceNamed.OriginalDefinition.SpecialType
                != SpecialType.System_Collections_Generic_IEnumerable_T
            )
            {
                continue;
            }

            if (ifaceNamed.TypeArguments.Length != 1)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(ifaceNamed.TypeArguments[0], elementType))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal",
        };
    }
}
