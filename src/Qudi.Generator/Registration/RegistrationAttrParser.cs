using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Registration;

internal static class RegistrationAttrParser
{
    private const string DefaultLifetime = "Singleton";

    private const string QudiAttribute = $"Qudi.QudiAttribute";
    private const string QudiSingletonAttribute = $"Qudi.DISingletonAttribute";
    private const string QudiScopedAttribute = $"Qudi.DIScopedAttribute";
    private const string QudiTransientAttribute = $"Qudi.DITransientAttribute";
    private const string QudiDecoratorAttribute = $"Qudi.QudiDecoratorAttribute";
    private const string QudiStrategyAttribute = $"Qudi.QudiStrategyAttribute";

    public static IncrementalValueProvider<
        ImmutableArray<RegistrationSpec?>
    > QudiAttributeRegistration(IncrementalGeneratorInitializationContext context)
    {
        var qudiProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context)
        );
        var singletonProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiSingletonAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context, lifetime: "Singleton")
        );
        var transientProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiTransientAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context, lifetime: "Transient")
        );
        var scopedProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiScopedAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context, lifetime: "Scoped")
        );
        var decoratorProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiDecoratorAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context, asDecorator: true)
        );
        var strategyProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiStrategyAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context, asStrategy: true)
        );

        var qudiRegistrations = qudiProvider.Collect();
        var singletonCollections = singletonProvider.Collect();
        var transientCollections = transientProvider.Collect();
        var scopedCollections = scopedProvider.Collect();
        var decoratorCollections = decoratorProvider.Collect();
        var strategyCollections = strategyProvider.Collect();
        return qudiRegistrations
            .CombineAndMerge(singletonCollections)
            .CombineAndMerge(transientCollections)
            .CombineAndMerge(scopedCollections)
            .CombineAndMerge(decoratorCollections)
            .CombineAndMerge(strategyCollections);
    }

    /// <summary>
    /// Create a RegistrationSpec instance from an attribute context.
    /// </summary>
    public static RegistrationSpec? CreateFromAttribute(
        GeneratorAttributeSyntaxContext context,
        string? lifetime = null,
        bool asDecorator = false,
        bool asStrategy = false
    )
    {
        // filter some invalid cases
        if (
            context.TargetSymbol is not INamedTypeSymbol typeSymbol
            || typeSymbol.IsAbstract
            || typeSymbol.TypeParameters.Length > 0
            || context.Attributes.Length == 0
        )
        {
            return null;
        }

        // parse attribute
        var attribute = context.Attributes[0];
        // fully qualified namespace
        var spec = CreateDefault(attribute);

        // overwrite with type information
        var typeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // determine required types (all constructor-injectable interfaces and classes)
        var requiredTypes = new EquatableArray<string>(
            typeSymbol
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Constructor)
                .SelectMany(m => m.Parameters)
                .Select(p => p.Type)
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<ITypeSymbol>()
                .Select(t => CodeGenerationUtility.ToTypeOfLiteral(t))
        );
        // default AsTypes to all implemented interfaces if not specified
        var defaultAsTypes = new EquatableArray<string>(
            typeSymbol.AllInterfaces.Select(CodeGenerationUtility.ToTypeOfLiteral)
        );

        return spec with
        {
            TypeName = typeFullName,
            Namespace = DetermineNamespace(typeSymbol),
            RequiredTypes = requiredTypes,
            AsTypes = spec.AsTypes.Count > 0 ? spec.AsTypes : defaultAsTypes,
            Lifetime = lifetime ?? spec.Lifetime,
            MarkAsDecorator = asDecorator || spec.MarkAsDecorator,
            MarkAsStrategy = asStrategy || spec.MarkAsStrategy,
        };
    }

    /// <summary>
    /// Create a default RegistrationSpec instance from an attribute.
    /// </summary>
    private static RegistrationSpec CreateDefault(AttributeData attr)
    {
        return new RegistrationSpec
        {
            // TypeName, Namespace, RequiredTypes is should be overwritten later.
            Lifetime = SGAttributeParser.GetValue<string>(attr, "Lifetime") ?? DefaultLifetime,
            When = SGAttributeParser.GetValues<string>(attr, "When"),
            AsTypes = SGAttributeParser.GetValueAsTypes(attr, "AsTypes"),
            UsePublic = SGAttributeParser.GetValue<bool?>(attr, "UsePublic") ?? true,
            KeyLiteral = SGAttributeParser.GetValueAsLiteral(attr, "Key"),
            Order = SGAttributeParser.GetValue<int?>(attr, "Order") ?? 0,
            MarkAsDecorator = SGAttributeParser.GetValue<bool?>(attr, "MarkAsDecorator") ?? false,
            MarkAsStrategy = SGAttributeParser.GetValue<bool?>(attr, "MarkAsStrategy") ?? false,
        };
    }

    private static string DetermineNamespace(INamedTypeSymbol typeSymbol)
    {
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        var isGlobal =
            typeSymbol.ContainingNamespace == null
            || typeSymbol.ContainingNamespace.IsGlobalNamespace;
        return (!isGlobal && ns != null) ? ns : string.Empty;
    }
}
