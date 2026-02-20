using System;
using System.Collections.Generic;
using System.Linq;

namespace Qudi.Core.Internal;

/// <summary>
/// Builds a normalized registration graph from collected registration metadata.
/// </summary>
public static class QudiRegistrationGraphBuilder
{
    /// <summary>
    /// Builds a graph that can be shared by DI containers and visualization services.
    /// </summary>
    public static QudiRegistrationGraph Build(QudiConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var allEntries = BuildEntries(configuration.Registrations, configuration.Conditions);
        var applicableEntries = allEntries.Where(e => e.IsConditionMatched).ToList();

        var materializedRegistrations = MaterializeOpenGenericFallbacks(
            applicableEntries.Select(e => e.Registration).ToList()
        );

        var materializedEntries = BuildEntries(materializedRegistrations, configuration.Conditions)
            .Where(e => e.IsConditionMatched)
            .ToList();

        var baseRegistrations = materializedEntries
            .Select(e => e.Registration)
            .Where(r => !r.MarkAsDecorator && (!r.MarkAsComposite || r.MarkAsDispatcher))
            .ToList();

        var layeredEntries = materializedEntries
            .Where(e =>
                e.Registration.MarkAsDecorator
                || (e.Registration.MarkAsComposite && !e.Registration.MarkAsDispatcher)
            )
            .OrderBy(e => e.Registration.Order)
            .ThenBy(e => e.Registration.MarkAsComposite ? 0 : 1)
            .ToList();

        var layeredRegistrations = layeredEntries.Select(e => e.Registration).ToList();

        var layersByService = layeredEntries
            .SelectMany(e => e.ServiceTypes.Select(serviceType => (Service: serviceType, Entry: e)))
            .GroupBy(x => x.Service)
            .ToDictionary(
                g => g.Key,
                g =>
                    (IReadOnlyList<QudiRegistrationEntry>)
                        g.Select(x => x.Entry)
                            .OrderBy(r => r.Registration.Order)
                            .ThenBy(r => r.Registration.MarkAsComposite ? 1 : 0)
                            .ThenBy(r => r.Registration.Type.FullName, StringComparer.Ordinal)
                            .ToList()
            );

        var implementationsByService = materializedEntries
            .Where(e => !e.Registration.MarkAsDecorator)
            .SelectMany(e => e.ServiceTypes.Select(serviceType => (Service: serviceType, Entry: e)))
            .GroupBy(x => x.Service)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<QudiRegistrationEntry>)g.Select(x => x.Entry).ToList()
            );

        var baseImplementationsByService = materializedEntries
            .Where(e => !e.Registration.MarkAsDecorator && !e.Registration.MarkAsComposite)
            .SelectMany(e => e.ServiceTypes.Select(serviceType => (Service: serviceType, Entry: e)))
            .GroupBy(x => x.Service)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<QudiRegistrationEntry>)g.Select(x => x.Entry).ToList()
            );

        var internalAssemblies = allEntries
            .Select(e => e.Registration.AssemblyName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToList();

        var availableTypes = GenericConstraintUtility.CollectLoadableTypes(
            configuration.Registrations.Select(r => r.Type.Assembly).Distinct().ToList()
        );

        return new QudiRegistrationGraph
        {
            AllEntries = allEntries,
            ApplicableEntries = applicableEntries,
            MaterializedEntries = materializedEntries,
            BaseRegistrations = baseRegistrations,
            LayeredRegistrations = layeredRegistrations,
            LayersByService = layersByService,
            ImplementationsByService = implementationsByService,
            BaseImplementationsByService = baseImplementationsByService,
            InternalAssemblies = internalAssemblies,
            AvailableTypes = availableTypes,
        };
    }

    /// <summary>
    /// Checks whether a registration is applicable for the current runtime conditions.
    /// </summary>
    public static bool IsConditionMatched(
        TypeRegistrationInfo registration,
        IReadOnlyCollection<string> conditions
    )
    {
        if (registration.When.Count == 0)
        {
            return true;
        }

        return registration.When.Any(r => conditions.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    private static List<QudiRegistrationEntry> BuildEntries(
        IEnumerable<TypeRegistrationInfo> registrations,
        IReadOnlyCollection<string> conditions
    )
    {
        return registrations
            .Select(registration =>
            {
                var serviceTypes = RegistrationTypeUtility
                    .GetEffectiveAsTypes(registration)
                    .Distinct()
                    .ToList();

                var matched = IsConditionMatched(registration, conditions);
                var conditionText =
                    registration.When.Count > 0 ? string.Join(", ", registration.When) : null;

                return new QudiRegistrationEntry
                {
                    Registration = registration,
                    ServiceTypes = serviceTypes,
                    IsConditionMatched = matched,
                    Condition = conditionText,
                };
            })
            .ToList();
    }

    private static List<TypeRegistrationInfo> MaterializeOpenGenericFallbacks(
        IReadOnlyCollection<TypeRegistrationInfo> registrations
    )
    {
        List<Type>? availableTypes = null;
        IReadOnlyList<Type> GetAvailableTypes() =>
            availableTypes ??= GenericConstraintUtility.CollectLoadableTypes(
                registrations.Select(r => r.Type.Assembly).Distinct().ToList()
            );

        var closedRegistrations = registrations
            .SelectMany(RegistrationTypeUtility.GetEffectiveAsTypes)
            .Where(t => !t.IsGenericTypeDefinition)
            .ToHashSet();

        var requiredTypes = registrations
            .SelectMany(r => r.RequiredTypes)
            .SelectMany(CollectAllTypes)
            .Where(t => t.IsConstructedGenericType)
            .ToList();

        var materialized = new List<TypeRegistrationInfo>();

        foreach (var registration in registrations)
        {
            var effectiveAsTypes = RegistrationTypeUtility.GetEffectiveAsTypes(registration);
            if (
                registration.Key is not null
                || !registration.Type.IsGenericTypeDefinition
                || effectiveAsTypes.Count == 0
            )
            {
                materialized.Add(registration);
                continue;
            }

            if (registration.MarkAsDispatcher)
            {
                materialized.Add(registration);
                continue;
            }

            var candidateAsTypes =
                registration.AsTypes.Count > 0
                    ? registration.AsTypes
                    : effectiveAsTypes.Where(t => t != registration.Type).ToList();

            var genericAsTypes = candidateAsTypes
                .Where(t => t.IsGenericTypeDefinition)
                .Distinct()
                .ToList();

            if (genericAsTypes.Count == 0)
            {
                materialized.Add(registration);
                continue;
            }

            var generated = false;

            foreach (var genericAsType in genericAsTypes)
            {
                List<Type> candidates;

                if (registration.MarkAsComposite && !registration.MarkAsDispatcher)
                {
                    candidates = closedRegistrations
                        .Where(t =>
                            t.IsConstructedGenericType
                            && t.GetGenericTypeDefinition() == genericAsType
                        )
                        .Distinct()
                        .ToList();

                    candidates.AddRange(
                        BuildConstraintBasedCandidates(
                            genericAsType,
                            GetAvailableTypes(),
                            includeAbstract: true,
                            includeInterfaces: true,
                            includeConstraintTypes: true
                        )
                    );

                    candidates = candidates.Distinct().ToList();
                }
                else
                {
                    candidates = requiredTypes
                        .Where(t => t.GetGenericTypeDefinition() == genericAsType)
                        .Distinct()
                        .ToList();

                    candidates.AddRange(
                        BuildConstraintBasedCandidates(
                            genericAsType,
                            GetAvailableTypes(),
                            includeAbstract: false,
                            includeInterfaces: false,
                            includeConstraintTypes: false
                        )
                    );

                    candidates = candidates.Distinct().ToList();
                }

                foreach (var candidate in candidates)
                {
                    if (!registration.MarkAsComposite && closedRegistrations.Contains(candidate))
                    {
                        continue;
                    }

                    Type closedImplementation;
                    try
                    {
                        closedImplementation = registration.Type.MakeGenericType(
                            candidate.GetGenericArguments()
                        );
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    List<Type> closedAsTypes;
                    try
                    {
                        closedAsTypes = effectiveAsTypes
                            .Select(t =>
                                t.IsGenericTypeDefinition
                                    ? t.MakeGenericType(candidate.GetGenericArguments())
                                    : t
                            )
                            .Distinct()
                            .ToList();
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    materialized.Add(
                        registration with
                        {
                            Type = closedImplementation,
                            AsTypes = closedAsTypes,
                        }
                    );

                    closedRegistrations.Add(candidate);
                    generated = true;
                }
            }

            if (!generated)
            {
                materialized.Add(registration);
            }
        }

        return materialized;
    }

    private static List<Type> BuildConstraintBasedCandidates(
        Type openGenericServiceType,
        IReadOnlyList<Type> availableTypes,
        bool includeAbstract,
        bool includeInterfaces,
        bool includeConstraintTypes
    )
    {
        if (!TryGetSingleGenericParameter(openGenericServiceType, out var genericParameter))
        {
            return [];
        }

        var constraints = genericParameter.GetGenericParameterConstraints();
        if (constraints.Length == 0)
        {
            return [];
        }

        var candidates = new List<Type>();

        foreach (var candidate in availableTypes)
        {
            if (candidate.ContainsGenericParameters || candidate.IsGenericTypeDefinition)
            {
                continue;
            }

            if (!includeInterfaces && candidate.IsInterface)
            {
                continue;
            }

            if (!includeAbstract && candidate.IsAbstract)
            {
                continue;
            }

            if (
                !GenericConstraintUtility.SatisfiesConstraints(
                    candidate,
                    genericParameter,
                    constraints
                )
            )
            {
                continue;
            }

            TryAddClosedCandidate(candidates, openGenericServiceType, candidate);
        }

        if (includeConstraintTypes)
        {
            foreach (var constraint in constraints)
            {
                if (constraint == typeof(object))
                {
                    continue;
                }

                if (
                    !GenericConstraintUtility.SatisfiesConstraints(
                        constraint,
                        genericParameter,
                        constraints
                    )
                )
                {
                    continue;
                }

                TryAddClosedCandidate(candidates, openGenericServiceType, constraint);
            }
        }

        return candidates.Distinct().ToList();
    }

    private static void TryAddClosedCandidate(
        ICollection<Type> candidates,
        Type openGenericServiceType,
        Type argumentType
    )
    {
        try
        {
            candidates.Add(openGenericServiceType.MakeGenericType(argumentType));
        }
        catch (ArgumentException)
        {
            // Ignore types that do not satisfy generic constraints.
        }
    }

    private static bool TryGetSingleGenericParameter(
        Type openGenericType,
        out Type genericParameter
    )
    {
        genericParameter = typeof(object);
        if (!openGenericType.IsGenericTypeDefinition)
        {
            return false;
        }

        var genericArguments = openGenericType.GetGenericArguments();
        if (genericArguments.Length != 1)
        {
            return false;
        }

        genericParameter = genericArguments[0];
        return genericParameter.IsGenericParameter;
    }

    private static IEnumerable<Type> CollectAllTypes(Type type)
    {
        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in CollectAllTypes(argument))
            {
                yield return nested;
            }
        }
    }
}
