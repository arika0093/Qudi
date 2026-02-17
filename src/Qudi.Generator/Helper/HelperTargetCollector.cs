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
    private const string QudiCompositeAttribute = "Qudi.QudiCompositeAttribute";
    private const string QudiDispatchAttribute = "Qudi.QudiDispatchAttribute";
    private const string CompositeMethodAttribute = "Qudi.CompositeMethodAttribute";

    public static IncrementalValueProvider<HelperGenerationInput> CollectTargets(
        IncrementalGeneratorInitializationContext context
    )
    {
        var decoratorTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiDecoratorAttribute,
                static (node, _) => IsPartialClass(node),
                static (ctx, _) => CreateTargets(ctx, isDecorator: true, isComposite: false, isDispatch: false)
            )
            .Select(static (targets, _) => targets);
        var compositeTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiCompositeAttribute,
                static (node, _) => IsPartialClass(node),
                static (ctx, _) => CreateTargets(ctx, isDecorator: false, isComposite: true, isDispatch: false)
            )
            .Select(static (targets, _) => targets);
        var dispatchTargets = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                QudiDispatchAttribute,
                static (node, _) => IsPartialClass(node),
                static (ctx, _) => CreateTargets(ctx, isDecorator: false, isComposite: true, isDispatch: true)
            )
            .Select(static (targets, _) => targets);
        return decoratorTargets
            .Collect()
            .Combine(compositeTargets.Collect())
            .Combine(dispatchTargets.Collect())
            .Select(
                static (targets, _) =>
                    MergeTargets(targets.Left.Left.Concat(targets.Left.Right).Concat(targets.Right).ToImmutableArray())
            );
    }

    // Check if the syntax node is a partial class declaration
    private static bool IsPartialClass(SyntaxNode node)
    {
        var classDecl = node as ClassDeclarationSyntax;
        return classDecl?.Modifiers.Any(SyntaxKind.PartialKeyword) == true;
    }

    private static HelperGenerationInput CreateTargets(
        GeneratorAttributeSyntaxContext context,
        bool isDecorator,
        bool isComposite,
        bool isDispatch
    )
    {
        var blankInput = new HelperGenerationInput
        {
            InterfaceTargets = new EquatableArray<HelperInterfaceTarget>([]),
            ImplementingTargets = new EquatableArray<HelperImplementingTarget>([]),
            DispatchCompositeTargets = new EquatableArray<DispatchCompositeTarget>([]),
        };

        // Validate target symbol and attributes
        if (
            context.TargetSymbol is not INamedTypeSymbol typeSymbol
            || context.Attributes.Length == 0
        )
        {
            return blankInput;
        }

        var attributeName = isDecorator ? QudiDecoratorAttribute : isDispatch ? QudiDispatchAttribute : QudiCompositeAttribute;
        var attribute = context
            .Attributes.Where(attr =>
                SymbolEqualityComparer.Default.Equals(
                    attr.AttributeClass,
                    context.SemanticModel.Compilation.GetTypeByMetadataName(attributeName)
                )
            )
            .FirstOrDefault();
        var asTypes = GetExplicitAsTypes(attribute);
        var useIntercept =
            isDecorator && (SGAttributeParser.GetValue<bool?>(attribute, "UseIntercept") ?? false);
        var dispatchMultiple = isDispatch
            ? (SGAttributeParser.GetValue<bool?>(attribute, "Multiple") ?? true)
            : true;

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
            var containingTypeKeyword =
                $"{(containingIsRecord ? "record " : "")}{containingTypeKind}";
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

        // Exclude System built-in types (IDisposable, IEquatable<T>, etc.) from auto-registration
        IEnumerable<INamedTypeSymbol> interfaces =
            asTypes.Length > 0
                ? asTypes
                : typeSymbol
                    .AllInterfaces.OfType<INamedTypeSymbol>()
                    .Where(i => !CodeGenerationUtility.IsSystemBuiltInType(i));
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
        var dispatchTargets = new List<DispatchCompositeTarget>();
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
                isComposite,
                useIntercept,
                containingTypesList
            );
            var members = CollectInterfaceMembers(iface).ToImmutableArray();
            if (constructorTarget is not null)
            {
                var compositeMethodOverrides = isComposite
                    ? CollectCompositeMethodOverrides(context, typeSymbol, members)
                    : ImmutableArray<CompositeMethodOverride>.Empty;
                implementingTargets.Add(constructorTarget);
                implementingTargets[^1] = implementingTargets[^1] with
                {
                    CompositeMethodOverrides = new EquatableArray<CompositeMethodOverride>(
                        compositeMethodOverrides
                    ),
                };
            }
            else if (isComposite && prunedInterfaces.Length == 1)
            {
                // Dispatch composites: no user-defined constructor, single generic interface.
                var dispatchTarget = TryCreateDispatchCompositeTarget(
                    context,
                    typeSymbol,
                    iface,
                    typeName,
                    typeNamespace,
                    typeKeyword,
                    containingTypesList,
                    members,
                    dispatchMultiple
                );
                if (dispatchTarget is not null)
                {
                    dispatchTargets.Add(dispatchTarget);
                }
            }
            var decoratorParameterName =
                isDecorator && constructorTarget is not null
                    ? constructorTarget.BaseParameterName
                    : string.Empty;

            // Extract generic type information from the interface
            var genericConstraints = CodeGenerationUtility.GetGenericConstraints(iface);
            var genericArgs = CodeGenerationUtility.GetGenericTypeArguments(iface);

            var target = new HelperInterfaceTarget
            {
                InterfaceName = interfaceName,
                InterfaceNamespace = interfaceNamespace,
                InterfaceHelperName = interfaceHelperName,
                HelperNamespaceSuffix = SanitizeIdentifier(interfaceName),
                DecoratorParameterName = decoratorParameterName,
                Members = new EquatableArray<HelperMember>(members),
                IsDecorator = isDecorator,
                IsComposite = isComposite,
                UseIntercept = useIntercept,
                GenericTypeParameters = genericConstraints, // Store the where clauses
                GenericTypeArguments = genericArgs,
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
            DispatchCompositeTargets = new EquatableArray<DispatchCompositeTarget>(
                dispatchTargets.ToImmutableArray()
            ),
        };
    }

    private static HelperGenerationInput MergeTargets(ImmutableArray<HelperGenerationInput> targets)
    {
        var interfaceTargets = targets.SelectMany(t => t.InterfaceTargets).ToImmutableArray();
        var implementingTargets = targets.SelectMany(t => t.ImplementingTargets).ToImmutableArray();
        var dispatchTargets = targets.SelectMany(t => t.DispatchCompositeTargets).ToImmutableArray();

        var mergedInterfaces = MergeInterfaceTargets(interfaceTargets);
        var mergedImplementing = MergeImplementingTargets(implementingTargets, mergedInterfaces);

        return new HelperGenerationInput
        {
            InterfaceTargets = new EquatableArray<HelperInterfaceTarget>(mergedInterfaces),
            ImplementingTargets = new EquatableArray<HelperImplementingTarget>(mergedImplementing),
            DispatchCompositeTargets = new EquatableArray<DispatchCompositeTarget>(
                MergeDispatchTargets(dispatchTargets)
            ),
        };
    }

    private static ImmutableArray<DispatchCompositeTarget> MergeDispatchTargets(
        ImmutableArray<DispatchCompositeTarget> targets
    )
    {
        if (targets.IsDefaultOrEmpty)
        {
            return ImmutableArray<DispatchCompositeTarget>.Empty;
        }

        var map = new Dictionary<string, DispatchCompositeTarget>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            var key =
                target.ImplementingTypeNamespace
                + "."
                + target.ImplementingTypeName
                + ":"
                + target.InterfaceName;
            if (!map.ContainsKey(key))
            {
                map[key] = target;
            }
        }

        return map.Values.ToImmutableArray();
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
            var key = BuildInterfaceTargetKey(target);
            if (!map.TryGetValue(key, out var existing))
            {
                map[key] = target;
                continue;
            }

            map[key] = existing with
            {
                IsDecorator = existing.IsDecorator || target.IsDecorator,
                IsComposite = existing.IsComposite || target.IsComposite,
                DecoratorParameterName = MergeParameterName(
                    existing.DecoratorParameterName,
                    target.DecoratorParameterName
                ),
                UseIntercept = existing.UseIntercept || target.UseIntercept,
            };
        }
        return map.Values.ToImmutableArray();
    }

    private static string BuildInterfaceTargetKey(HelperInterfaceTarget target)
    {
        // Separate helper interfaces for decorator vs composite to avoid name collisions.
        return BuildInterfaceTargetKey(target.InterfaceName, target.IsComposite);
    }

    private static string BuildInterfaceTargetKey(string interfaceName, bool isComposite)
    {
        // Separate helper interfaces for decorator vs composite to avoid name collisions.
        return $"{interfaceName}::{(isComposite ? "composite" : "decorator")}";
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
            target => BuildInterfaceTargetKey(target),
            target => target.UseIntercept,
            StringComparer.Ordinal
        );
        var map = new Dictionary<string, HelperImplementingTarget>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            var useIntercept = useInterceptByInterface.TryGetValue(
                BuildInterfaceTargetKey(target.InterfaceName, target.IsComposite),
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
                CompositeMethodOverrides = new EquatableArray<CompositeMethodOverride>(
                    existing
                        .CompositeMethodOverrides.Concat(target.CompositeMethodOverrides)
                        .Distinct()
                        .ToImmutableArray()
                ),
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

        foreach (var iface in interfaceSymbol.AllInterfaces.Append(interfaceSymbol))
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
        bool isComposite,
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

        // Extract generic type information from the implementing type
        var genericConstraints = CodeGenerationUtility.GetGenericConstraints(typeSymbol);
        var genericArgs = CodeGenerationUtility.GetGenericTypeArguments(typeSymbol);

        return new HelperImplementingTarget
        {
            ImplementingTypeName = typeName,
            ImplementingTypeNamespace = typeNamespace,
            ImplementingTypeKeyword = typeKeyword,
            ContainingTypes = new EquatableArray<ContainingTypeInfo>(
                containingTypes.ToImmutableArray()
            ),
            ConstructorAccessibility = accessibility,
            InterfaceName = interfaceName,
            InterfaceNamespace = interfaceNamespace,
            InterfaceHelperName = interfaceHelperName,
            ConstructorParameters = new EquatableArray<HelperParameter>(constructorParameters),
            BaseParameterName = baseParameter.Name,
            IsDecorator = isDecorator,
            IsComposite = isComposite,
            UseIntercept = useIntercept,
            GenericTypeParameters = genericConstraints, // Store the where clauses
            GenericTypeArguments = genericArgs,
            CompositeMethodOverrides = new EquatableArray<CompositeMethodOverride>([]),
        };
    }

    private static ImmutableArray<CompositeMethodOverride> CollectCompositeMethodOverrides(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol typeSymbol,
        ImmutableArray<HelperMember> interfaceMembers
    )
    {
        if (interfaceMembers.IsDefaultOrEmpty)
        {
            return ImmutableArray<CompositeMethodOverride>.Empty;
        }

        var interfaceMethods = interfaceMembers
            .Where(m => m.Kind == HelperMemberKind.Method)
            .ToImmutableArray();
        if (interfaceMethods.IsDefaultOrEmpty)
        {
            return ImmutableArray<CompositeMethodOverride>.Empty;
        }

        var compositeMethodAttrSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(
            CompositeMethodAttribute
        );
        var methods = typeSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m =>
                m.MethodKind == MethodKind.Ordinary
                && !m.IsStatic
                && m.IsPartialDefinition
                && m.PartialImplementationPart is null
            );

        var result = new List<CompositeMethodOverride>();
        foreach (var method in methods)
        {
            var methodMember = interfaceMethods.FirstOrDefault(im => IsSameSignature(im, method));
            if (methodMember is null)
            {
                continue;
            }

            var methodResult = CompositeResultBehavior.All;
            if (compositeMethodAttrSymbol is not null)
            {
                var attribute = method
                    .GetAttributes()
                    .FirstOrDefault(attr =>
                        SymbolEqualityComparer.Default.Equals(
                            attr.AttributeClass,
                            compositeMethodAttrSymbol
                        )
                    );
                if (attribute is not null)
                {
                    foreach (var argument in attribute.NamedArguments)
                    {
                        if (argument.Key == "Result" && argument.Value.Value is int intResult)
                        {
                            methodResult = intResult switch
                            {
                                0 => CompositeResultBehavior.All,
                                1 => CompositeResultBehavior.Any,
                                2 => CompositeResultBehavior.Sequential,
                                _ => CompositeResultBehavior.All,
                            };
                        }
                    }
                }
            }

            result.Add(
                new CompositeMethodOverride
                {
                    Name = method.Name,
                    ReturnTypeName = method.ReturnType.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat
                    ),
                    Parameters = new EquatableArray<HelperParameter>(
                        method.Parameters.Select(CreateParameter).ToImmutableArray()
                    ),
                    ResultBehavior = methodResult,
                }
            );
        }

        return result.ToImmutableArray();
    }

    private static bool IsSameSignature(HelperMember helperMember, IMethodSymbol method)
    {
        if (!string.Equals(helperMember.Name, method.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (
            !string.Equals(
                helperMember.ReturnTypeName,
                method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                StringComparison.Ordinal
            )
        )
        {
            return false;
        }

        if (helperMember.Parameters.Count != method.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var left = helperMember.Parameters[i];
            var right = method.Parameters[i];
            var rightType = right.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!string.Equals(left.TypeName, rightType, StringComparison.Ordinal))
            {
                return false;
            }

            if (
                !string.Equals(
                    left.RefKindPrefix,
                    GetRefKindPrefix(right.RefKind),
                    StringComparison.Ordinal
                )
            )
            {
                return false;
            }
        }

        return true;
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

        // Check for IEnumerable<T> where T is assignable to the interface (for composites)
        if (parameterType is INamedTypeSymbol namedType)
        {
            // Check if this is IEnumerable<T> by comparing the original definition
            if (
                namedType.IsGenericType
                && namedType.OriginalDefinition.ToDisplayString()
                    == "System.Collections.Generic.IEnumerable<T>"
            )
            {
                var elementType = namedType.TypeArguments.FirstOrDefault();
                if (
                    elementType is not null
                    && (
                        SymbolEqualityComparer.Default.Equals(elementType, interfaceSymbol)
                        || (
                            elementType is INamedTypeSymbol elementNamedType
                            && elementNamedType.AllInterfaces.Any(iface =>
                                SymbolEqualityComparer.Default.Equals(iface, interfaceSymbol)
                            )
                        )
                    )
                )
                {
                    return true;
                }
            }

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

    private static DispatchCompositeTarget? TryCreateDispatchCompositeTarget(
        GeneratorAttributeSyntaxContext context,
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol interfaceSymbol,
        string typeName,
        string typeNamespace,
        string typeKeyword,
        List<ContainingTypeInfo> containingTypes,
        ImmutableArray<HelperMember> members,
        bool multiple
    )
    {
        if (!typeSymbol.IsGenericType || typeSymbol.TypeParameters.Length != 1)
        {
            return null;
        }

        if (
            !interfaceSymbol.IsGenericType
            || interfaceSymbol.TypeArguments.Length != 1
            || interfaceSymbol.TypeArguments[0] is not ITypeParameterSymbol ifaceParam
        )
        {
            return null;
        }

        var typeParam = typeSymbol.TypeParameters[0];
        if (!SymbolEqualityComparer.Default.Equals(ifaceParam, typeParam))
        {
            return null;
        }

        // Dispatch composite uses generated constructor; skip if user already defined one.
        if (typeSymbol.InstanceConstructors.Any(ctor => !ctor.IsImplicitlyDeclared))
        {
            return null;
        }

        // Require constraints so we can enumerate valid dispatch targets.
        if (typeParam.ConstraintTypes.IsEmpty)
        {
            return null;
        }

        // Concrete types come from constraint matching within the current compilation.
        var concreteTypes = CollectConcreteTypes(context.SemanticModel.Compilation, typeParam);
        if (concreteTypes.IsDefaultOrEmpty)
        {
            return null;
        }

        var dispatchMethods = CollectDispatchCompositeMethods(
            interfaceSymbol,
            members,
            typeParam
        );

        var compositeMethodOverrides = CollectCompositeMethodOverrides(
            context,
            typeSymbol,
            members
        );

        // Create a field/parameter per concrete type so dispatchers can inject
        // IEnumerable<IComponentValidator<Concrete>> for each matching type.
        var concreteTypeInfos = new List<DispatchCompositeConcreteType>();
        var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var concreteType in concreteTypes)
        {
            var constructedInterface = interfaceSymbol.OriginalDefinition.Construct(concreteType);
            var constructedInterfaceName = constructedInterface.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );
            var typeDisplayName = concreteType.ToDisplayString(
                SymbolDisplayFormat.MinimallyQualifiedFormat
            );
            var baseName = SanitizeIdentifier(typeDisplayName);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Type";
            }

            var fieldName = $"_{char.ToLowerInvariant(baseName[0])}{baseName.Substring(1)}Validators";
            if (!usedFieldNames.Add(fieldName))
            {
                var suffix = 1;
                string candidate;
                do
                {
                    candidate = $"{fieldName}{suffix}";
                    suffix++;
                } while (!usedFieldNames.Add(candidate));

                fieldName = candidate;
            }

            var parameterName = fieldName.TrimStart('_');

            concreteTypeInfos.Add(
                new DispatchCompositeConcreteType
                {
                    TypeName = concreteType.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat
                    ),
                    FieldName = fieldName,
                    ParameterName = parameterName,
                    ConstructedInterfaceTypeName = constructedInterfaceName,
                }
            );
        }

        var genericConstraints = CodeGenerationUtility.GetGenericConstraints(typeSymbol);
        var genericArgs = CodeGenerationUtility.GetGenericTypeArguments(typeSymbol);
        var interfaceHelperName = SanitizeIdentifier(
            interfaceSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        );

        // Dispatchers are generated for each constraint type (e.g., IComponent) so
        // the container can resolve IComponentValidator<IComponent> directly.
        var constraintTypeInfos = new List<DispatchCompositeConstraintType>();
        foreach (var constraint in typeParam.ConstraintTypes.OfType<INamedTypeSymbol>())
        {
            if (constraint.SpecialType == SpecialType.System_Object)
            {
                continue;
            }

            var constraintInterface = interfaceSymbol.OriginalDefinition.Construct(constraint);
            var suffix = SanitizeIdentifier(
                constraint.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            );
            constraintTypeInfos.Add(
                new DispatchCompositeConstraintType
                {
                    TypeName = constraint.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    Suffix = suffix,
                    ConstructedInterfaceTypeName = constraintInterface.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat
                    ),
                }
            );
        }

        return new DispatchCompositeTarget
        {
            ImplementingTypeName = typeName,
            ImplementingTypeNamespace = typeNamespace,
            ImplementingTypeKeyword = typeKeyword,
            ImplementingTypeAccessibility = GetAccessibility(typeSymbol.DeclaredAccessibility),
            ContainingTypes = new EquatableArray<ContainingTypeInfo>(containingTypes.ToImmutableArray()),
            InterfaceName = interfaceSymbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            ),
            InterfaceHelperName = interfaceHelperName,
            Methods = new EquatableArray<DispatchCompositeMethod>(dispatchMethods),
            GenericTypeParameters = genericConstraints,
            GenericTypeArguments = genericArgs,
            ConcreteTypes = new EquatableArray<DispatchCompositeConcreteType>(
                concreteTypeInfos.ToImmutableArray()
            ),
            ConstraintTypes = new EquatableArray<DispatchCompositeConstraintType>(
                constraintTypeInfos.ToImmutableArray()
            ),
            Multiple = multiple,
            CompositeMethodOverrides = new EquatableArray<CompositeMethodOverride>(
                compositeMethodOverrides
            ),
        };
    }

    /// <summary>
    /// Collects concrete types from <paramref name="compilation"/> by scanning only
    /// <c>compilation.Assembly.GlobalNamespace</c>.
    /// This current-compilation-only scope is intentional to keep discovery predictable
    /// and avoid introducing external assembly dependencies during source generation.
    /// </summary>
    private static ImmutableArray<INamedTypeSymbol> CollectConcreteTypes(
        Compilation compilation,
        ITypeParameterSymbol typeParam
    )
    {
        var constraints = typeParam.ConstraintTypes;
        if (constraints.IsEmpty)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var allTypes = new List<INamedTypeSymbol>();
        CollectTypes(compilation.Assembly.GlobalNamespace, allTypes);

        var results = allTypes
            .Where(type => type.TypeKind is TypeKind.Class or TypeKind.Struct)
            .Where(type => !type.IsAbstract && !type.IsGenericType)
            .Where(type => SatisfiesConstraints(type, typeParam, constraints))
            .Distinct(NamedTypeSymbolComparer.Instance)
            .ToImmutableArray();

        return results;
    }

    private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> results)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            results.Add(type);
            foreach (var nested in GetNestedTypes(type))
            {
                results.Add(nested);
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            CollectTypes(child, results);
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var sub in GetNestedTypes(nested))
            {
                yield return sub;
            }
        }
    }

    private static bool SatisfiesConstraints(
        INamedTypeSymbol candidate,
        ITypeParameterSymbol typeParam,
        ImmutableArray<ITypeSymbol> constraints
    )
    {
        if (typeParam.HasReferenceTypeConstraint && candidate.IsValueType)
        {
            return false;
        }

        if (typeParam.HasValueTypeConstraint && !candidate.IsValueType)
        {
            return false;
        }

        if (
            typeParam.HasConstructorConstraint
            && !candidate.IsValueType
            && candidate.InstanceConstructors.All(c => c.Parameters.Length > 0)
        )
        {
            return false;
        }

        return constraints.All(constraint => IsAssignable(candidate, constraint));
    }

    private static bool IsAssignable(INamedTypeSymbol candidate, ITypeSymbol constraint)
    {
        if (SymbolEqualityComparer.Default.Equals(candidate, constraint))
        {
            return true;
        }

        if (constraint.TypeKind == TypeKind.Interface)
        {
            return candidate.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i, constraint)
            );
        }

        var current = candidate.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, constraint))
            {
                return true;
            }
            current = current.BaseType;
        }

        return false;
    }

    private static ImmutableArray<DispatchCompositeMethod> CollectDispatchCompositeMethods(
        INamedTypeSymbol interfaceSymbol,
        ImmutableArray<HelperMember> members,
        ITypeParameterSymbol typeParam
    )
    {
        var methods = interfaceSymbol
            .AllInterfaces.Append(interfaceSymbol)
            .SelectMany(i => i.GetMembers())
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .ToImmutableArray();

        var results = new List<DispatchCompositeMethod>();
        foreach (var member in members.Where(m => m.Kind == HelperMemberKind.Method))
        {
            var methodSymbol = methods.FirstOrDefault(m =>
            {
                if (!string.Equals(member.Name, m.Name, StringComparison.Ordinal))
                {
                    return false;
                }

                if (m.Parameters.Length != member.Parameters.Count)
                {
                    return false;
                }

                for (var i = 0; i < m.Parameters.Length; i++)
                {
                    var symbolParameter = m.Parameters[i];
                    var memberParameter = member.Parameters[i];

                    if (
                        !string.Equals(
                            symbolParameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            memberParameter.TypeName,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        return false;
                    }

                    if (
                        !string.Equals(
                            GetRefKindPrefix(symbolParameter.RefKind),
                            memberParameter.RefKindPrefix ?? string.Empty,
                            StringComparison.Ordinal
                        )
                    )
                    {
                        return false;
                    }
                }

                return true;
            });

            var index = -1;
            if (methodSymbol is not null)
            {
                for (var i = 0; i < methodSymbol.Parameters.Length; i++)
                {
                    var parameterType = methodSymbol.Parameters[i].Type;
                    if (SymbolEqualityComparer.Default.Equals(parameterType, typeParam))
                    {
                        index = i;
                        break;
                    }
                }
            }

            results.Add(new DispatchCompositeMethod { Member = member, DispatchParameterIndex = index });
        }

        return results.ToImmutableArray();
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
