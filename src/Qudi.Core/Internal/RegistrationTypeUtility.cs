using System;
using System.Collections.Generic;
using System.Linq;

namespace Qudi.Core.Internal;

/// <summary>
/// Utility class for processing type registration information.
/// </summary>
public static class RegistrationTypeUtility
{
    /// <summary>
    /// Get the effective AsTypes for a registration, considering the AsTypesFallback strategy if AsTypes is not explicitly specified.
    /// </summary>
    public static IReadOnlyList<Type> GetEffectiveAsTypes(TypeRegistrationInfo registration)
    {
        if (registration.AsTypes.Count > 0)
        {
            return registration.AsTypes;
        }

        return registration.AsTypesFallback switch
        {
            AsTypesFallback.Self => [registration.Type],
            AsTypesFallback.Interfaces => GetFilteredInterfaces(registration.Type),
            AsTypesFallback.SelfWithInterface => Combine(
                registration.Type,
                GetFilteredInterfaces(registration.Type)
            ),
            _ => [registration.Type],
        };
    }

    private static IReadOnlyList<Type> GetFilteredInterfaces(Type type)
    {
        return type.GetInterfaces()
            .Where(i => !IsSystemBuiltInType(i))
            .Select(EnsureOpenGenericDefinition)
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<Type> Combine(Type type, IReadOnlyList<Type> interfaces)
    {
        if (interfaces.Count == 0)
        {
            return [type];
        }

        var list = new List<Type>(interfaces.Count + 1) { type };
        list.AddRange(interfaces);
        return list.Distinct().ToList();
    }

    private static bool IsSystemBuiltInType(Type type)
    {
        var ns = type.Namespace;
        return ns != null && ns.StartsWith("System", StringComparison.Ordinal);
    }

    private static Type EnsureOpenGenericDefinition(Type type)
    {
        if (type.IsGenericType && type.ContainsGenericParameters)
        {
            return type.GetGenericTypeDefinition();
        }

        return type;
    }
}
