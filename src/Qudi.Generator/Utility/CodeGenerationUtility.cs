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
}
