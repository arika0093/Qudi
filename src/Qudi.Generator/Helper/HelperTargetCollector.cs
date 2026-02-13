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

    public static IncrementalValueProvider<HelperGenerationInput> CollectTargets(
        IncrementalGeneratorInitializationContext context
    )
    {
        var decoratorTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiDecoratorAttribute,
                static (node, _) => IsPartialClass(node),
                static (ctx, _) => CreateTargets(ctx, isDecorator: true)
            )
            .Select(static (targets, _) => targets);
        return decoratorTargets.Collect().Select(static (targets, _) => MergeTargets(targets));
    }

    // Check if the syntax node is a partial class declaration
    private static bool IsPartialClass(SyntaxNode node)
    {
        var classDecl = node as ClassDeclarationSyntax;
        return classDecl?.Modifiers.Any(SyntaxKind.PartialKeyword) == true;
    }

    private static HelperGenerationInput CreateTargets(
        GeneratorAttributeSyntaxContext context,
        bool isDecorator
    )
    {
        var blankInput = new HelperGenerationInput
        {
            InterfaceTargets = new EquatableArray<HelperInterfaceTarget>([]),
            ImplementingTargets = new EquatableArray<HelperImplementingTarget>([]),
        };

        // Validate target symbol and attributes
        if (
            context.TargetSymbol is not INamedTypeSymbol typeSymbol
            || context.Attributes.Length == 0
        )
        {
            return blankInput;
        }

        var attribute = context
            .Attributes.Where(attr =>
                SymbolEqualityComparer.Default.Equals(
                    attr.AttributeClass,
                    context.SemanticModel.Compilation.GetTypeByMetadataName(QudiDecoratorAttribute)
                )
            )
            .FirstOrDefault();
        var asTypes = GetExplicitAsTypes(attribute);
        var useIntercept = SGAttributeParser.GetValue<bool?>(attribute, "UseIntercept") ?? false;

        // Collect nested class information (from innermost to outermost)
        var containingTypesList = new List<ContainingTypeInfo>();
        var currentType = typeSymbol.ContainingType;
        while (currentType is not null)
        {
            var containingTypeKind = currentType.TypeKind switch
            {
                TypeKind.Class => "class",
                TypeKind.Struct => "struct",
                _ => "class",
            };
            var containingIsRecord = currentType.IsRecord;
            var containingTypeKeyword = $"{(containingIsRecord ? "record " : "")}{containingTypeKind}";
            var accessibility = GetAccessibility(currentType.DeclaredAccessibility);

            containingTypesList.Add(
                new ContainingTypeInfo
                {
                    Name = currentType.Name,
                    TypeKeyword = containingTypeKeyword,
                    Accessibility = accessibility,
                }
            );
            currentType = currentType.ContainingType;
        }
        // Reverse to get from outermost to innermost
        containingTypesList.Reverse();

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
        var interfaceList = interfaces
            .Where(iface => iface.TypeKind == TypeKind.Interface)
            .Distinct(NamedTypeSymbolComparer.Instance)
            .ToImmutableArray();
        if (interfaceList.IsDefaultOrEmpty)
        {
            return blankInput;
        }

        if (useIntercept)
        {
            // TODO: If multiple interfaces are specified, it is desirable to issue a warning with the analyzer.
            if (asTypes.Length > 0)
            {
                interfaceList = ImmutableArray.Create(asTypes[0]);
            }
            else
            {
                var first = interfaces.FirstOrDefault();
                interfaceList = first is null
                    ? ImmutableArray<INamedTypeSymbol>.Empty
                    : ImmutableArray.Create(first);
            }
        }

        var prunedInterfaces = FilterDerivedInterfaces(interfaceList);
        var interfaceTargets = new List<HelperInterfaceTarget>();
        var implementingTargets = new List<HelperImplementingTarget>();
        foreach (
            var iface in prunedInterfaces.OrderBy(iface =>
                iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            )
        )
        {
            var interfaceName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var interfaceNamespace = iface.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : iface.ContainingNamespace.ToDisplayString();
            var interfaceHelperName = SanitizeIdentifier(
                iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            );
            var constructorTarget = FindPartialConstructorTarget(
                context,
                typeName,
                typeNamespace,
                typeKeyword,
                interfaceName,
                interfaceNamespace,
                interfaceHelperName,
                typeSymbol,
                iface,
                isDecorator,
                useIntercept,
                containingTypesList
            );
            if (constructorTarget is not null)
            {
                implementingTargets.Add(constructorTarget);
            }
            var members = CollectInterfaceMembers(iface);
            var decoratorParameterName =
                isDecorator && constructorTarget is not null
                    ? constructorTarget.BaseParameterName
                    : string.Empty;
            var target = new HelperInterfaceTarget
            {
                InterfaceName = interfaceName,
                InterfaceNamespace = interfaceNamespace,
                InterfaceHelperName = interfaceHelperName,
                HelperNamespaceSuffix = SanitizeIdentifier(interfaceName),
                DecoratorParameterName = decoratorParameterName,
                Members = new EquatableArray<HelperMember>(members),
                IsDecorator = isDecorator,
                UseIntercept = useIntercept,
            };
            interfaceTargets.Add(target);
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

    private static HelperGenerationInput MergeTargets(ImmutableArray<HelperGenerationInput> targets)
    {
        var interfaceTargets = targets.SelectMany(t => t.InterfaceTargets).ToImmutableArray();
        var implementingTargets = targets.SelectMany(t => t.ImplementingTargets).ToImmutableArray();

        var mergedInterfaces = MergeInterfaceTargets(interfaceTargets);
        var mergedImplementing = MergeImplementingTargets(implementingTargets, mergedInterfaces);

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
                DecoratorParameterName = MergeParameterName(
                    existing.DecoratorParameterName,
                    target.DecoratorParameterName
                ),
                UseIntercept = existing.UseIntercept || target.UseIntercept,
            };
        }
        return map.Values.ToImmutableArray();
    }

    private static string MergeParameterName(string existing, string incoming)
    {
        var hasExisting = !string.IsNullOrEmpty(existing);
        var hasIncoming = !string.IsNullOrEmpty(incoming);
        if (!hasExisting)
        {
            return incoming;
        }

        if (!hasIncoming)
        {
            return existing;
        }

        return string.Equals(existing, incoming, StringComparison.Ordinal)
            ? existing
            : string.Empty;
    }

    private static ImmutableArray<HelperImplementingTarget> MergeImplementingTargets(
        ImmutableArray<HelperImplementingTarget> targets,
        ImmutableArray<HelperInterfaceTarget> interfaceTargets
    )
    {
        if (targets.IsDefaultOrEmpty)
        {
            return ImmutableArray<HelperImplementingTarget>.Empty;
        }

        var useInterceptByInterface = interfaceTargets.ToDictionary(
            target => target.InterfaceName,
            target => target.UseIntercept,
            StringComparer.Ordinal
        );
        var map = new Dictionary<string, HelperImplementingTarget>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            var useIntercept = useInterceptByInterface.TryGetValue(
                target.InterfaceName,
                out var use
            )
                ? use
                : target.UseIntercept;
            var key =
                target.ImplementingTypeNamespace
                + "."
                + target.ImplementingTypeName
                + ":"
                + target.InterfaceName;
            if (!map.TryGetValue(key, out var existing))
            {
                map[key] = target with { UseIntercept = useIntercept };
                continue;
            }

            map[key] = existing with
            {
                IsDecorator = existing.IsDecorator || target.IsDecorator,
                UseIntercept = existing.UseIntercept || useIntercept,
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

    private static ImmutableArray<INamedTypeSymbol> FilterDerivedInterfaces(
        ImmutableArray<INamedTypeSymbol> interfaces
    )
    {
        if (interfaces.Length <= 1)
        {
            return interfaces;
        }

        var list = new List<INamedTypeSymbol>();
        foreach (var iface in interfaces)
        {
            var isBase = interfaces.Any(other =>
                !SymbolEqualityComparer.Default.Equals(other, iface)
                && other.AllInterfaces.Any(inherited =>
                    SymbolEqualityComparer.Default.Equals(inherited, iface)
                )
            );
            if (!isBase)
            {
                list.Add(iface);
            }
        }

        return list.ToImmutableArray();
    }

    private static IEnumerable<HelperMember> CollectInterfaceMembers(
        INamedTypeSymbol interfaceSymbol
    )
    {
        var members = new List<HelperMember>();
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var iface in interfaceSymbol.AllInterfaces.Concat([interfaceSymbol]))
        {
            foreach (var member in iface.GetMembers())
            {
                if (!visited.Add(member))
                {
                    continue;
                }

                if (!IsVisibleInHelper(member))
                {
                    continue;
                }

                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    members.Add(CreateMethodMember(method, iface));
                    continue;
                }

                if (member is IPropertySymbol property)
                {
                    members.Add(CreatePropertyMember(property, iface));
                }
            }
        }

        return members;
    }

    private static bool IsVisibleInHelper(ISymbol member)
    {
        return member.DeclaredAccessibility
            is Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal
                or Accessibility.ProtectedAndInternal;
    }

    private static HelperMember CreateMethodMember(
        IMethodSymbol method,
        INamedTypeSymbol declaringInterface
    )
    {
        var parameters = method.Parameters.Select(CreateParameter).ToImmutableArray();
        return new HelperMember()
        {
            Kind = HelperMemberKind.Method,
            Name = method.Name,
            ReturnTypeName = method.ReturnType.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            DeclaringInterfaceName = declaringInterface.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            Parameters = new EquatableArray<HelperParameter>(parameters),
            HasGetter = false,
            HasSetter = false,
            IsIndexer = false,
        };
    }

    private static HelperMember CreatePropertyMember(
        IPropertySymbol property,
        INamedTypeSymbol declaringInterface
    )
    {
        var parameters = property.Parameters.Select(CreateParameter).ToImmutableArray();
        return new HelperMember()
        {
            Kind = HelperMemberKind.Property,
            Name = property.Name,
            ReturnTypeName = property.Type.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            DeclaringInterfaceName = declaringInterface.ToDisplayString(
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
        string interfaceName,
        string interfaceNamespace,
        string interfaceHelperName,
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol interfaceSymbol,
        bool isDecorator,
        bool useIntercept,
        List<ContainingTypeInfo> containingTypes
    )
    {
        if (context.TargetNode is not ClassDeclarationSyntax)
        {
            return null;
        }

        var ctorCandidates = typeSymbol
            .InstanceConstructors.Where(ctor =>
                !ctor.IsStatic
                && ctor.Parameters.Length > 0
                && ctor.Parameters.Any(parameter =>
                    IsAssignableToInterface(parameter.Type, interfaceSymbol)
                )
            )
            .ToArray();
        if (ctorCandidates.Length == 0)
        {
            return null;
        }

        var ctorSymbol =
            ctorCandidates.FirstOrDefault(ctor => !ctor.IsImplicitlyDeclared) ?? ctorCandidates[0];
        var baseParameter = FindParameterForInterface(ctorSymbol.Parameters, interfaceSymbol);
        if (baseParameter is null)
        {
            return null;
        }

        var constructorParameters = ctorSymbol
            .Parameters.Select(parameter => new HelperParameter
            {
                TypeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Name = parameter.Name,
                RefKindPrefix = GetRefKindPrefix(parameter.RefKind),
                IsParams = parameter.IsParams,
            })
            .ToArray();
        var accessibility = GetAccessibility(ctorSymbol.DeclaredAccessibility);

        if (baseParameter is null)
        {
            return null;
        }

        return new HelperImplementingTarget
        {
            ImplementingTypeName = typeName,
            ImplementingTypeNamespace = typeNamespace,
            ImplementingTypeKeyword = typeKeyword,
            ContainingTypes = new EquatableArray<ContainingTypeInfo>(containingTypes.ToImmutableArray()),
            ConstructorAccessibility = accessibility,
            InterfaceName = interfaceName,
            InterfaceNamespace = interfaceNamespace,
            InterfaceHelperName = interfaceHelperName,
            ConstructorParameters = new EquatableArray<HelperParameter>(constructorParameters),
            BaseParameterName = baseParameter.Name,
            IsDecorator = isDecorator,
            UseIntercept = useIntercept,
        };
    }

    private static IParameterSymbol? FindParameterForInterface(
        ImmutableArray<IParameterSymbol> parameters,
        INamedTypeSymbol interfaceSymbol
    )
    {
        var exact = parameters.FirstOrDefault(parameter =>
            SymbolEqualityComparer.Default.Equals(parameter.Type, interfaceSymbol)
        );
        if (exact is not null)
        {
            return exact;
        }

        return parameters.FirstOrDefault(parameter =>
            IsAssignableToInterface(parameter.Type, interfaceSymbol)
        );
    }

    private static bool IsAssignableToInterface(
        ITypeSymbol parameterType,
        INamedTypeSymbol interfaceSymbol
    )
    {
        if (SymbolEqualityComparer.Default.Equals(parameterType, interfaceSymbol))
        {
            return true;
        }

        if (parameterType is INamedTypeSymbol namedType)
        {
            return namedType.AllInterfaces.Any(iface =>
                SymbolEqualityComparer.Default.Equals(iface, interfaceSymbol)
            );
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

internal sealed class NamedTypeSymbolComparer : IEqualityComparer<INamedTypeSymbol>
{
    public static readonly NamedTypeSymbolComparer Instance = new();

    public bool Equals(INamedTypeSymbol? x, INamedTypeSymbol? y)
    {
        return SymbolEqualityComparer.Default.Equals(x, y);
    }

    public int GetHashCode(INamedTypeSymbol obj)
    {
        return SymbolEqualityComparer.Default.GetHashCode(obj);
    }
}
