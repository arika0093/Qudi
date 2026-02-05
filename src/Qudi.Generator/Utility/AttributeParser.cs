using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

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
        var value = GetValue<object?>(attribute, name);
        return CodeGenerationUtility.ToLiteral(value);
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
