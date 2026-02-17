using System;
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

        var whereClauses = new System.Collections.Generic.List<string>();
        foreach (var typeParam in typeSymbol.TypeParameters)
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

            if (constraints.Count > 0)
            {
                whereClauses.Add($"where {typeParam.Name} : {string.Join(", ", constraints)}");
            }
        }

        return whereClauses.Count > 0 ? string.Join(" ", whereClauses) : string.Empty;
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
}
