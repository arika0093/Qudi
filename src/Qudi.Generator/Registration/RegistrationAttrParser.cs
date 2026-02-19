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
    private const int DefaultDuplicate = 3; // DuplicateHandling.Add
    private const int DefaultAsTypesFallback = 2; // AsTypesFallback.SelfWithInterfaces

    private const string QudiAttribute = $"Qudi.QudiAttribute";
    private const string QudiSingletonAttribute = $"Qudi.DISingletonAttribute";
    private const string QudiScopedAttribute = $"Qudi.DIScopedAttribute";
    private const string QudiTransientAttribute = $"Qudi.DITransientAttribute";
    private const string QudiDecoratorAttribute = $"Qudi.QudiDecoratorAttribute";
    private const string QudiCompositeAttribute = $"Qudi.QudiCompositeAttribute";
    private const string QudiDispatchAttribute = $"Qudi.QudiDispatchAttribute";

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
        var compositeProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiCompositeAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context, asComposite: true)
        );
        var dispatchProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            QudiDispatchAttribute,
            static (node, _) => true,
            static (context, _) => CreateFromAttribute(context, asDispatch: true)
        );

        var qudiRegistrations = qudiProvider.Collect();
        var singletonCollections = singletonProvider.Collect();
        var transientCollections = transientProvider.Collect();
        var scopedCollections = scopedProvider.Collect();
        var decoratorCollections = decoratorProvider.Collect();
        var compositeCollections = compositeProvider.Collect();
        var dispatchCollections = dispatchProvider.Collect();
        return qudiRegistrations
            .CombineAndMerge(singletonCollections)
            .CombineAndMerge(transientCollections)
            .CombineAndMerge(scopedCollections)
            .CombineAndMerge(decoratorCollections)
            .CombineAndMerge(compositeCollections)
            .CombineAndMerge(dispatchCollections);
    }

    /// <summary>
    /// Create a RegistrationSpec instance from an attribute context.
    /// </summary>
    public static RegistrationSpec? CreateFromAttribute(
        GeneratorAttributeSyntaxContext context,
        string? lifetime = null,
        bool asDecorator = false,
        bool asComposite = false,
        bool asDispatch = false
    )
    {
        // filter some invalid cases
        if (
            context.TargetSymbol is not INamedTypeSymbol typeSymbol
            || typeSymbol.IsAbstract
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
        var typeFullName = CodeGenerationUtility.ToTypeName(typeSymbol);
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
                .Where(t => !ContainsTypeParameters(t))
                .Select(t => CodeGenerationUtility.ToTypeOfLiteral(t))
        );
        var isDecorator = asDecorator || spec.MarkAsDecorator;
        var isComposite = asComposite || spec.MarkAsComposite;
        var isDispatchComposite = asDispatch;
        // Dispatch entries are marked as dispatcher registrations so container layering can skip composite wrapping.
        var isCompositeDispatcher = isDispatchComposite;
        var effectiveLifetime =
            (isDecorator || isComposite || isDispatchComposite)
                ? "Transient"
                : lifetime ?? spec.Lifetime;

        return spec with
        {
            TypeName = typeFullName,
            Namespace = DetermineNamespace(typeSymbol),
            RequiredTypes = requiredTypes,
            AsTypes = spec.AsTypes,
            Lifetime = effectiveLifetime,
            MarkAsDecorator = isDecorator,
            MarkAsComposite = isComposite,
            MarkAsDispatcher = isCompositeDispatcher,
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
            Duplicate = SGAttributeParser.GetValueAsInt(attr, "Duplicate") ?? DefaultDuplicate,
            AsTypesFallback =
                SGAttributeParser.GetValueAsInt(attr, "AsTypesFallback") ?? DefaultAsTypesFallback,
            UsePublic = SGAttributeParser.GetValue<bool?>(attr, "UsePublic") ?? true,
            KeyLiteral = SGAttributeParser.GetValueAsLiteral(attr, "Key"),
            Order = SGAttributeParser.GetValue<int?>(attr, "Order") ?? 0,
            MarkAsDecorator = SGAttributeParser.GetValue<bool?>(attr, "MarkAsDecorator") ?? false,
            MarkAsComposite = SGAttributeParser.GetValue<bool?>(attr, "MarkAsComposite") ?? false,
            Export = SGAttributeParser.GetValue<bool?>(attr, "Export") ?? false,
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

    private static bool ContainsTypeParameters(ITypeSymbol symbol)
    {
        if (symbol is ITypeParameterSymbol)
        {
            return true;
        }

        if (symbol is IArrayTypeSymbol arrayType)
        {
            return ContainsTypeParameters(arrayType.ElementType);
        }

        if (symbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            return namedType.TypeArguments.Any(ContainsTypeParameters);
        }

        return false;
    }

    // Composite dispatch registration is handled by generated types.
}
