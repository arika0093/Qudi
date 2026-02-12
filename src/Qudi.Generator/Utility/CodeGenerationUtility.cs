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
}
