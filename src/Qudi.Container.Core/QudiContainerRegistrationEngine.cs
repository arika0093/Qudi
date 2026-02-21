using System;
using System.Collections.Generic;
using System.Linq;
using Qudi;
using Qudi.Core.Internal;

namespace Qudi.Container.Core;

/// <summary>
/// Shared registration engine that applies normalized Qudi registration graphs to DI container adapters.
/// </summary>
public static class QudiContainerRegistrationEngine
{
    /// <summary>
    /// Registers non-layered services from the graph.
    /// </summary>
    public static void RegisterBaseServices(
        IQudiContainerRegistrationAdapter adapter,
        IReadOnlyList<TypeRegistrationInfo> registrations
    )
    {
        if (adapter is null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        if (registrations is null)
        {
            throw new ArgumentNullException(nameof(registrations));
        }

        foreach (var registration in registrations)
        {
            RegisterService(adapter, registration);
        }
    }

    /// <summary>
    /// Applies decorator/composite layers from the graph.
    /// </summary>
    public static void ApplyLayeredRegistrations<TDescriptor>(
        IQudiLayeredRegistrationAdapter<TDescriptor> adapter,
        IReadOnlyDictionary<Type, IReadOnlyList<QudiRegistrationEntry>> layersByService
    )
    {
        if (adapter is null)
        {
            throw new ArgumentNullException(nameof(adapter));
        }

        if (layersByService is null)
        {
            throw new ArgumentNullException(nameof(layersByService));
        }

        foreach (var (serviceType, layers) in layersByService)
        {
            if (layers.Count == 0)
            {
                continue;
            }

            if (!adapter.SupportsKeyedLayers && layers.Any(r => r.Registration.Key is not null))
            {
                throw new InvalidOperationException(
                    $"Keyed layered registrations are not supported by {adapter.GetType().Name}."
                );
            }

            var currentDescriptors = adapter.GetServiceDescriptors(serviceType).ToList();
            if (currentDescriptors.Count == 0)
            {
                var hasComposite = layers.Any(r => r.Registration.MarkAsComposite);
                var hasDecorator = layers.Any(r => r.Registration.MarkAsDecorator);
                if (hasDecorator && !hasComposite)
                {
                    throw new InvalidOperationException(
                        $"No base registration found for {serviceType.FullName}. A decorator was registered without any base implementation."
                    );
                }
            }

            for (var i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i].Registration;
                if (layer.MarkAsDecorator)
                {
                    currentDescriptors = currentDescriptors
                        .Select(d => adapter.DescribeDecorator(serviceType, d, layer))
                        .ToList();
                    continue;
                }

                if (layer.MarkAsComposite)
                {
                    currentDescriptors = [adapter.DescribeComposite(serviceType, layer, currentDescriptors)];
                }
            }

            adapter.ReplaceServiceDescriptors(serviceType, currentDescriptors);
        }
    }

    private static void RegisterService(
        IQudiContainerRegistrationAdapter adapter,
        TypeRegistrationInfo registration
    )
    {
        var lifetime = registration.Lifetime;
        var isOpenGeneric = registration.Type.IsGenericTypeDefinition;
        var serviceTypes = RegistrationTypeUtility.GetEffectiveAsTypes(registration);
        var registerSelf = serviceTypes.Contains(registration.Type);

        if (serviceTypes.Count == 0)
        {
            return;
        }

        if (registration.Key is null)
        {
            if (registerSelf)
            {
                adapter.TryAddService(
                    new QudiServiceRegistrationRequest
                    {
                        ServiceType = registration.Type,
                        ImplementationType = registration.Type,
                        Lifetime = lifetime,
                        DuplicateHandling = registration.Duplicate,
                        Kind = QudiServiceRegistrationKind.ImplementationType,
                    }
                );
            }

            if (isOpenGeneric || serviceTypes.Any(t => t.IsGenericTypeDefinition))
            {
                foreach (var asType in serviceTypes)
                {
                    if (asType == registration.Type)
                    {
                        continue;
                    }

                    adapter.TryAddService(
                        new QudiServiceRegistrationRequest
                        {
                            ServiceType = asType,
                            ImplementationType = registration.Type,
                            Lifetime = lifetime,
                            DuplicateHandling = registration.Duplicate,
                            Kind = QudiServiceRegistrationKind.ImplementationType,
                        }
                    );
                }

                return;
            }

            foreach (var asType in serviceTypes)
            {
                if (asType == registration.Type)
                {
                    continue;
                }

                adapter.TryAddService(
                    new QudiServiceRegistrationRequest
                    {
                        ServiceType = asType,
                        ImplementationType = registration.Type,
                        Lifetime = lifetime,
                        DuplicateHandling = registration.Duplicate,
                        Kind = registerSelf
                            ? QudiServiceRegistrationKind.ForwardToImplementation
                            : QudiServiceRegistrationKind.ImplementationType,
                    }
                );
            }

            return;
        }

        if (
            registration.AsTypes.Count == 0
            && serviceTypes.Count == 1
            && serviceTypes[0] == registration.Type
        )
        {
            adapter.TryAddService(
                new QudiServiceRegistrationRequest
                {
                    ServiceType = registration.Type,
                    ImplementationType = registration.Type,
                    Lifetime = lifetime,
                    DuplicateHandling = registration.Duplicate,
                    Kind = QudiServiceRegistrationKind.ImplementationType,
                    Key = registration.Key,
                }
            );
            return;
        }

        foreach (var asType in serviceTypes)
        {
            adapter.TryAddService(
                new QudiServiceRegistrationRequest
                {
                    ServiceType = asType,
                    ImplementationType = registration.Type,
                    Lifetime = lifetime,
                    DuplicateHandling = registration.Duplicate,
                    Kind = QudiServiceRegistrationKind.ImplementationType,
                    Key = registration.Key,
                }
            );
        }
    }
}
