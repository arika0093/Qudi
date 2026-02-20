using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Qudi.Generator;

/// <summary>
/// Utility methods for code generation.
/// </summary>
internal static class CodeGenerationUtility
{
    public static string? ToLiteral(object? value)
    {
        return value?.ToString() ?? null;
    }

    public static string ToTypeOfLiteral(ITypeSymbol typeSymbol)
    {
        var fullName = ToTypeName(typeSymbol);
        return $"typeof({fullName})";
    }

    public static string ToTypeName(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol named && named.IsGenericType)
        {
            var target = named;
            if (named.TypeArguments.Any(arg => arg is ITypeParameterSymbol))
            {
                target = named.ConstructUnboundGenericType();
            }

            return target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Determines if a type is a system built-in type that should be excluded from auto-registration.
    /// This includes interfaces from System namespaces like IDisposable, IEquatable{T}, IComparable{T}, etc.
    /// </summary>
    public static bool IsSystemBuiltInType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
        {
            return false;
        }

        // Get the containing namespace
        var ns = typeSymbol.ContainingNamespace;
        if (ns == null || ns.IsGlobalNamespace)
        {
            return false;
        }

        // Build the full namespace string
        var namespaceString = ns.ToDisplayString();

        // Exclude types from System.* namespaces
        // This includes System, System.Collections, System.Collections.Generic, System.Text, etc.
        return namespaceString.StartsWith("System", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts generic type parameter names only (e.g., "T" or "T, U").
    /// Returns empty string if the type has no generic parameters.
    /// </summary>
    public static string GetGenericTypeParameters(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol == null || !typeSymbol.IsGenericType || typeSymbol.TypeParameters.IsEmpty)
        {
            return string.Empty;
        }

        return string.Join(", ", typeSymbol.TypeParameters.Select(p => p.Name));
    }

    /// <summary>
    /// Gets the where clause constraints for generic type parameters (e.g., "where T : IComponent").
    /// Returns empty string if there are no constraints.
    /// </summary>
    public static string GetGenericConstraints(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol == null || !typeSymbol.IsGenericType || typeSymbol.TypeParameters.IsEmpty)
        {
            return string.Empty;
        }

        return BuildGenericConstraints(typeSymbol.TypeParameters);
    }

    /// <summary>
    /// Gets the generic type arguments list (e.g., "<T, U>").
    /// Returns empty string if the type has no generic parameters.
    /// </summary>
    public static string GetGenericTypeArguments(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol == null || !typeSymbol.IsGenericType || typeSymbol.TypeParameters.IsEmpty)
        {
            return string.Empty;
        }

        var typeArgs = string.Join(", ", typeSymbol.TypeParameters.Select(p => p.Name));
        return $"<{typeArgs}>";
    }

    /// <summary>
    /// Gets the generic type arguments for helper interfaces based on type arguments that are
    /// type parameters (e.g., "<T>" from IFoo&lt;T, int&gt;).
    /// Returns empty string if there are no type-parameter arguments.
    /// </summary>
    public static string GetHelperGenericTypeArguments(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol == null || !typeSymbol.IsGenericType || typeSymbol.TypeArguments.IsEmpty)
        {
            return string.Empty;
        }

        var names = typeSymbol
            .TypeArguments.OfType<ITypeParameterSymbol>()
            .Select(p => p.Name)
            .Distinct()
            .ToArray();
        if (names.Length == 0)
        {
            return string.Empty;
        }

        return $"<{string.Join(", ", names)}>";
    }

    /// <summary>
    /// Gets the where clause constraints for helper interfaces, mapped to the type-parameter
    /// arguments used by the constructed type (e.g., "where T : class").
    /// Returns empty string if there are no type-parameter arguments or constraints.
    /// </summary>
    public static string GetHelperGenericConstraints(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol == null || !typeSymbol.IsGenericType || typeSymbol.TypeArguments.IsEmpty)
        {
            return string.Empty;
        }

        var definition = typeSymbol.OriginalDefinition;
        if (definition.TypeParameters.IsEmpty)
        {
            return string.Empty;
        }

        var whereClauses = new System.Collections.Generic.List<string>();
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var typeArguments = typeSymbol.TypeArguments;
        for (var i = 0; i < typeArguments.Length && i < definition.TypeParameters.Length; i++)
        {
            if (typeArguments[i] is not ITypeParameterSymbol typeArg)
            {
                continue;
            }

            if (!seen.Add(typeArg.Name))
            {
                continue;
            }

            var constraints = BuildGenericConstraintList(definition.TypeParameters[i]);
            if (constraints.Count > 0)
            {
                whereClauses.Add($"where {typeArg.Name} : {string.Join(", ", constraints)}");
            }
        }

        return whereClauses.Count > 0 ? string.Join(" ", whereClauses) : string.Empty;
    }

    private static string BuildGenericConstraints(ImmutableArray<ITypeParameterSymbol> parameters)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return string.Empty;
        }

        var whereClauses = new System.Collections.Generic.List<string>();
        foreach (var typeParam in parameters)
        {
            var constraints = BuildGenericConstraintList(typeParam);
            if (constraints.Count > 0)
            {
                whereClauses.Add($"where {typeParam.Name} : {string.Join(", ", constraints)}");
            }
        }

        return whereClauses.Count > 0 ? string.Join(" ", whereClauses) : string.Empty;
    }

    private static System.Collections.Generic.List<string> BuildGenericConstraintList(
        ITypeParameterSymbol typeParam
    )
    {
        var constraints = new System.Collections.Generic.List<string>();

        // Add class/struct constraints
        if (typeParam.HasReferenceTypeConstraint)
        {
            constraints.Add("class");
        }
        if (typeParam.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        if (typeParam.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        if (typeParam.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        // Add type constraints
        foreach (var constraint in typeParam.ConstraintTypes)
        {
            constraints.Add(constraint.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        // Add constructor constraint
        if (typeParam.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints;
    }
}
