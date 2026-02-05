using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Registration;

internal static class RegistrationAttrParser
{
    private const string QudiAttribute = $"Qudi.QudiRegistrationAttribute";
    private const string QudiSingletonAttribute = $"Qudi.DISingletonAttribute";
    private const string QudiScopedAttribute = $"Qudi.DIScopedAttribute";
    private const string QudiTransientAttribute = $"Qudi.DITransientAttribute";
    private const string QudiDecoratorAttribute = $"Qudi.QudiDecoratorAttribute";

    public static IncrementalValueProvider<
        ImmutableArray<RegistrationSpec?>
    > QudiAttributeRegistration(IncrementalGeneratorInitializationContext context)
    {
        var qudiProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (context, _) => CreateFromAttribute(context)
        );
        var singletonProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiSingletonAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (context, _) => CreateFromAttribute(context, lifetime: "Singleton")
        );
        var transientProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiTransientAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (context, _) => CreateFromAttribute(context, lifetime: "Transient")
        );
        var scopedProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiScopedAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (context, _) => CreateFromAttribute(context, lifetime: "Scoped")
        );
        var decoratorProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiDecoratorAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            static (context, _) => CreateFromAttribute(context, asDecorator: true)
        );

        var qudiRegistrations = qudiProvider.Collect();
        var singletonCollections = singletonProvider.Collect();
        var transientCollections = transientProvider.Collect();
        var scopedCollections = scopedProvider.Collect();
        var decoratorCollections = decoratorProvider.Collect();
        return qudiRegistrations
            .CombineAndMerge(singletonCollections)
            .CombineAndMerge(transientCollections)
            .CombineAndMerge(scopedCollections)
            .CombineAndMerge(decoratorCollections);
    }

    /// <summary>
    /// Create a RegistrationSpec instance from an attribute context.
    /// </summary>
    public static RegistrationSpec? CreateFromAttribute(
        GeneratorAttributeSyntaxContext context,
        string? lifetime = null,
        bool asDecorator = false
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
        var spec = CreateDefault(attribute);

        // overwrite with type information
        var typeFullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var defaultAsTypes = new EquatableArray<string>(
            typeSymbol.AllInterfaces.Select(CodeGenerationUtility.ToTypeOfLiteral)
        );

        return spec with
        {
            TypeName = typeFullName,
            AsTypes = spec.AsTypes.Count > 0 ? spec.AsTypes : defaultAsTypes,
            Lifetime = lifetime ?? spec.Lifetime,
            MarkAsDecorator = asDecorator || spec.MarkAsDecorator,
        };
    }

    /// <summary>
    /// Create a default RegistrationSpec instance from an attribute.
    /// </summary>
    private static RegistrationSpec CreateDefault(AttributeData attr)
    {
        return new RegistrationSpec
        {
            // TypeName is should be overwritten later.
            Lifetime = SGAttributeParser.GetValueAsLiteral(attr, "Lifetime") ?? "",
            When = SGAttributeParser.GetValues<string>(attr, "When"),
            AsTypes = SGAttributeParser.GetValueAsTypes(attr, "AsTypes"),
            UsePublic = SGAttributeParser.GetValue<bool?>(attr, "UsePublic") ?? true,
            KeyLiteral = SGAttributeParser.GetValueAsLiteral(attr, "Key"),
            Order = SGAttributeParser.GetValue<int?>(attr, "Order") ?? 0,
            MarkAsDecorator = SGAttributeParser.GetValue<bool?>(attr, "MarkAsDecorator") ?? false,
        };
    }
}
