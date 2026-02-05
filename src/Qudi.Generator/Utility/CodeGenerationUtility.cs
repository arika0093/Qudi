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
        var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"typeof({fullName})";
    }
}
