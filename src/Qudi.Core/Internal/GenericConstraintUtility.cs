using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Qudi.Core.Internal;

/// <summary>
/// Provides utility methods for checking generic type parameter constraints.
/// </summary>
public static class GenericConstraintUtility
{
    /// <summary>
    /// Checks whether a candidate type satisfies the constraints of a generic parameter.
    /// </summary>
    /// <param name="candidate">The type to check against the constraints.</param>
    /// <param name="genericParameter">The generic parameter with constraints to check.</param>
    /// <param name="constraints">The array of constraint types.</param>
    /// <returns>True if the candidate satisfies all constraints; otherwise, false.</returns>
    public static bool SatisfiesConstraints(
        Type candidate,
        Type genericParameter,
        Type[] constraints
    )
    {
        var attributes = genericParameter.GenericParameterAttributes;

        if (
            attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint)
            && candidate.IsValueType
        )
        {
            return false;
        }

        if (
            attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)
            && !candidate.IsValueType
        )
        {
            return false;
        }

        if (
            attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
            && !candidate.IsValueType
            && candidate.GetConstructor(Type.EmptyTypes) is null
        )
        {
            return false;
        }

        foreach (var constraint in constraints)
        {
            if (constraint == typeof(object))
            {
                continue;
            }

            if (!constraint.IsAssignableFrom(candidate))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Collects all loadable types from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to load types from.</param>
    /// <returns>A list of all successfully loaded types from the assemblies.</returns>
    public static List<Type> CollectLoadableTypes(IReadOnlyList<Assembly> assemblies)
    {
        var types = new List<Type>();

        foreach (var assembly in assemblies)
        {
            try
            {
                types.AddRange(assembly.GetTypes());
            }
            catch (ReflectionTypeLoadException ex)
            {
                types.AddRange(ex.Types.Where(t => t is not null)!.Select(t => t!));
            }
        }

        return types;
    }
}
