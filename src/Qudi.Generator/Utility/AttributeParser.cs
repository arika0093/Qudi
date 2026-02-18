using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qudi.Generator.Utility;

internal static class SGAttributeParser
{
    /// <summary>
    /// Get a named argument value from an attribute.
    /// </summary>
    public static T? GetValue<T>(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Value is T value)
            {
                return value;
            }
        }
        return default;
    }

    /// <summary>
    /// Get a named argument value as type literal string from an attribute.
    /// </summary>
    public static string GetValueAsType(AttributeData attribute, string name)
    {
        var typeSymbol = GetValue<ITypeSymbol>(attribute, name);
        return CodeGenerationUtility.ToTypeOfLiteral(typeSymbol!);
    }

    /// <summary>
    /// Get a named argument value as string from an attribute.
    /// </summary>
    public static string? GetValueAsLiteral(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                return TypedConstantToLiteral(argument.Value);
            }
        }
        return null;
    }

    /// <summary>
    /// Get a named argument value as int from an attribute (supports enum values).
    /// </summary>
    public static int? GetValueAsInt(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key != name)
            {
                continue;
            }

            if (argument.Value.Value is int intValue)
            {
                return intValue;
            }

            if (argument.Value.Value is null)
            {
                return null;
            }

            if (argument.Value.Value is IConvertible convertible)
            {
                return Convert.ToInt32(convertible, CultureInfo.InvariantCulture);
            }
        }
        return null;
    }

    private static string TypedConstantToLiteral(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return "null";
        }

        if (constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol typeSymbol)
        {
            var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"typeof({fullName})";
        }

        if (constant.Type?.TypeKind == TypeKind.Enum && constant.Type is INamedTypeSymbol enumType)
        {
            var member = enumType
                .GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(m => m.HasConstantValue && Equals(m.ConstantValue, constant.Value));
            if (member is not null)
            {
                var enumName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return $"{enumName}.{member.Name}";
            }
            var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var primitive = SymbolDisplay.FormatPrimitive(
                constant.Value!,
                quoteStrings: true,
                useHexadecimalNumbers: false
            );
            return $"({enumTypeName}){primitive}";
        }

        return SymbolDisplay.FormatPrimitive(
            constant.Value!,
            quoteStrings: true,
            useHexadecimalNumbers: false
        );
    }

    /// <summary>
    /// Get an array of named argument values from an attribute.
    /// </summary>
    public static EquatableArray<T> GetValues<T>(AttributeData attribute, string name)
        where T : IEquatable<T>
    {
        return new EquatableArray<T>(GetValuesInternal<T>(attribute, name));
    }

    /// <summary>
    /// Get an array of type literal strings from an attribute named argument.
    /// </summary>
    public static EquatableArray<string> GetValueAsTypes(AttributeData attribute, string name)
    {
        var values = GetValuesInternal<ITypeSymbol>(attribute, name);
        var valuesAsString = values.Select(CodeGenerationUtility.ToTypeOfLiteral);
        return new EquatableArray<string>(valuesAsString);
    }

    //  Internal helper to get array values.
    private static IEnumerable<T> GetValuesInternal<T>(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name && argument.Value.Values.Length > 0)
            {
                var values = new List<T>();
                foreach (var item in argument.Value.Values)
                {
                    if (item.Value is T value)
                    {
                        values.Add(value);
                    }
                }
                return values;
            }
        }
        return [];
    }
}
