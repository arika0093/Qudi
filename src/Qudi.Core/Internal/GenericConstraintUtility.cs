using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Qudi.Core.Internal;

internal static class GenericConstraintUtility
{
    public static bool SatisfiesConstraints(Type candidate, Type genericParameter, Type[] constraints)
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
