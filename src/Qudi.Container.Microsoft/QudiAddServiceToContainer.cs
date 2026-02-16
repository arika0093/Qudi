using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace Qudi.Container.Microsoft;

/// <summary>
/// Provides extension methods for registering services in Qudi.
/// </summary>
public static class QudiAddServiceToContainer
{
    // caches for singleton and scoped instances
    private static readonly ConditionalWeakTable<
        IServiceScopeFactory,
        ConditionalWeakTable<ServiceDescriptor, object>
    > SingletonCache = new();

    private static readonly ConditionalWeakTable<
        IServiceProvider,
        ConditionalWeakTable<ServiceDescriptor, object>
    > ScopedCache = new();

    /// <summary>
    /// Registers Qudi-collected service definitions into <paramref name="services" />.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Runtime registration configuration.</param>
    public static IServiceCollection AddQudiServices(
        IServiceCollection services,
        QudiConfiguration configuration
    )
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // TODO: The logic here needs to be clearly refactored.
        var applicable = configuration
            .Registrations.Where(t => ShouldRegister(t, configuration.Conditions))
            .OrderBy(t => IsOpenGenericRegistration(t) ? 0 : 1)
            .ThenBy(t => t.Order)
            .ToList();

        var materialized = MaterializeOpenGenericFallbacks(applicable);

        var registrations = materialized
            .Where(t => !t.MarkAsDecorator && !t.MarkAsComposite)
            .ToList();
        var layeredRegistrations = materialized
            .Where(t => t.MarkAsDecorator || t.MarkAsComposite)
            // Higher order is applied later (outer), so process lower order first.
            .OrderBy(t => t.Order)
            // Keep composites ahead of decorators when Order is the same so decorators wrap composites.
            .ThenBy(t => t.MarkAsComposite ? 0 : 1)
            .ToList();

        foreach (var registration in registrations)
        {
            RegisterService(services, registration);
        }

        ApplyLayeredRegistrations(services, layeredRegistrations);

        return services;
    }

    private static void ApplyLayeredRegistrations(
        IServiceCollection services,
        IReadOnlyList<TypeRegistrationInfo> layeredRegistrations
    )
    {
        var byService = layeredRegistrations
            .SelectMany(reg =>
            {
                var serviceTypes = reg.AsTypes.Count > 0 ? reg.AsTypes : [reg.Type];
                return serviceTypes.Select(serviceType => (Service: serviceType, Reg: reg));
            })
            .GroupBy(x => x.Service)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Reg)
                    .OrderBy(r => r.Order)
                    // Lower order is outer; for the same order, decorators wrap composites.
                    .ThenBy(r => r.MarkAsComposite ? 1 : 0)
                    .ToList()
            );

        foreach (var (serviceType, layers) in byService)
        {
            if (layers.Count == 0)
            {
                continue;
            }

            if (layers.Any(r => r.Key is not null))
            {
                // TODO: Keyed decorators/composites are not supported yet.
                continue;
            }

            var descriptorIndexes = new List<int>();
            for (var i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceType == serviceType)
                {
                    descriptorIndexes.Add(i);
                }
            }

            var currentDescriptors = descriptorIndexes.Select(i => services[i]).ToList();
            if (descriptorIndexes.Count == 0)
            {
                if (!layers.Any(r => r.MarkAsComposite))
                {
                    continue;
                }
            }

            for (var i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer.MarkAsDecorator)
                {
                    currentDescriptors = currentDescriptors
                        .Select(d => DescribeDecoratedDescriptor(serviceType, d, layer))
                        .ToList();
                    continue;
                }

                if (layer.MarkAsComposite)
                {
                    currentDescriptors = [
                        DescribeCompositeDescriptor(serviceType, layer, currentDescriptors),
                    ];
                }
            }

            // Remove existing descriptors for this service type (from last to first index)
            for (var i = descriptorIndexes.Count - 1; i >= 0; i--)
            {
                services.RemoveAt(descriptorIndexes[i]);
            }

            foreach (var descriptor in currentDescriptors)
            {
                services.Add(descriptor);
            }
        }
    }

    private static ServiceDescriptor DescribeCompositeDescriptor(
        Type serviceType,
        TypeRegistrationInfo composite,
        IReadOnlyList<ServiceDescriptor> innerDescriptors
    )
    {
        return ServiceDescriptor.Describe(
            serviceType,
            sp =>
            {
                var innerServices = new object?[innerDescriptors.Count];
                for (var i = 0; i < innerDescriptors.Count; i++)
                {
                    innerServices[i] = CreateFromDescriptor(sp, innerDescriptors[i]);
                }

                var serviceArray = Array.CreateInstance(serviceType, innerServices.Length);
                for (var i = 0; i < innerServices.Length; i++)
                {
                    serviceArray.SetValue(innerServices[i], i);
                }

                return ActivatorUtilities.CreateInstance(sp, composite.Type, serviceArray);
            },
            ServiceLifetime.Transient
        );
    }

    private static List<TypeRegistrationInfo> MaterializeOpenGenericFallbacks(
        IReadOnlyCollection<TypeRegistrationInfo> registrations
    )
    {
        var closedRegistrations = registrations
            .SelectMany(r => r.AsTypes)
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
            if (
                registration.Key is not null
                || !registration.Type.IsGenericTypeDefinition
                || registration.AsTypes.Count == 0
            )
            {
                materialized.Add(registration);
                continue;
            }

            var genericAsTypes = registration
                .AsTypes.Where(t => t.IsGenericTypeDefinition)
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
                var candidates = requiredTypes
                    .Where(t => t.GetGenericTypeDefinition() == genericAsType)
                    .Distinct()
                    .ToList();

                foreach (var candidate in candidates)
                {
                    if (closedRegistrations.Contains(candidate))
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
                        closedAsTypes = registration
                            .AsTypes.Select(t =>
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

    private static bool ShouldRegister(
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

    private static bool IsOpenGenericRegistration(TypeRegistrationInfo registration)
    {
        if (registration.Type.IsGenericTypeDefinition)
        {
            return true;
        }

        return registration.AsTypes.Any(t => t.IsGenericTypeDefinition);
    }

    private static void RegisterService(
        IServiceCollection services,
        TypeRegistrationInfo registration
    )
    {
        // Register implementation first, then map interfaces to the same instance path.
        var lifetime = ConvertLifetime(registration.Lifetime);
        var isOpenGeneric = registration.Type.IsGenericTypeDefinition;

        object Factory(IServiceProvider sp) =>
            ActivatorUtilities.CreateInstance(sp, registration.Type);

        if (registration.AsTypes.Count == 0)
        {
            if (isOpenGeneric)
            {
                services.Add(new ServiceDescriptor(registration.Type, registration.Type, lifetime));
            }
            else
            {
                services.Add(ServiceDescriptor.Describe(registration.Type, Factory, lifetime));
            }
            return;
        }

        if (registration.Key is null)
        {
            if (isOpenGeneric)
            {
                services.Add(new ServiceDescriptor(registration.Type, registration.Type, lifetime));
            }
            else
            {
                services.Add(ServiceDescriptor.Describe(registration.Type, Factory, lifetime));
            }

            if (isOpenGeneric || registration.AsTypes.Any(t => t.IsGenericTypeDefinition))
            {
                foreach (var asType in registration.AsTypes)
                {
                    if (asType == registration.Type)
                    {
                        continue;
                    }

                    services.Add(new ServiceDescriptor(asType, registration.Type, lifetime));
                }

                return;
            }

            foreach (var asType in registration.AsTypes)
            {
                if (asType == registration.Type)
                {
                    continue;
                }

                services.Add(
                    ServiceDescriptor.Describe(
                        asType,
                        sp => sp.GetRequiredService(registration.Type),
                        lifetime
                    )
                );
            }

            return;
        }

        foreach (var asType in registration.AsTypes)
        {
            AddKeyedService(services, asType, registration.Type, registration.Key, lifetime);
        }
    }

    private static void RegisterComposite(
        IServiceCollection services,
        TypeRegistrationInfo composite
    )
    {
        // Composites need special handling to avoid circular dependencies.
        // Strategy: Manually resolve all non-composite implementations and pass them to the composite
        var lifetime = ServiceLifetime.Transient;

        if (composite.AsTypes.Count == 0)
        {
            // Register without interface - just the composite itself
            services.Add(
                ServiceDescriptor.Describe(
                    composite.Type,
                    sp => ActivatorUtilities.CreateInstance(sp, composite.Type),
                    lifetime
                )
            );
            return;
        }

        // For composites, we need to avoid circular dependency by providing
        // a factory that gets all existing implementations except the composite itself

        foreach (var asType in composite.AsTypes)
        {
            if (asType == composite.Type)
            {
                continue;
            }

            var capturedAsType = asType; // Capture for closure

            // Collect all existing service descriptors for this type (before adding the composite)
            var existingDescriptors = services.Where(d => d.ServiceType == capturedAsType).ToList();

            services.Add(
                ServiceDescriptor.Describe(
                    asType,
                    sp =>
                    {
                        // Manually resolve all the existing (non-composite) implementations
                        var nonCompositeServices = new List<object>();
                        foreach (var descriptor in existingDescriptors)
                        {
                            object? service = null;
                            if (descriptor.ImplementationType != null)
                            {
                                service = sp.GetRequiredService(descriptor.ImplementationType);
                            }
                            else if (descriptor.ImplementationFactory != null)
                            {
                                service = descriptor.ImplementationFactory(sp);
                            }
                            else
                            {
                                service = descriptor.ImplementationInstance;
                            }

                            if (service != null)
                            {
                                nonCompositeServices.Add(service);
                            }
                        }

                        // Create a strongly-typed array so IEnumerable<T> constructors match
                        var serviceArray = Array.CreateInstance(
                            capturedAsType,
                            nonCompositeServices.Count
                        );
                        for (var i = 0; i < nonCompositeServices.Count; i++)
                        {
                            serviceArray.SetValue(nonCompositeServices[i], i);
                        }

                        // Create the composite using the non-composite services
                        return ActivatorUtilities.CreateInstance(sp, composite.Type, serviceArray);
                    },
                    lifetime
                )
            );
        }
    }

    private static void ApplyDecorator(IServiceCollection services, TypeRegistrationInfo decorator)
    {
        // Decorators wrap every registration for each service type.
        if (decorator.AsTypes.Count == 0)
        {
            return;
        }

        if (decorator.Key is not null)
        {
            // TODO: Keyed decorators are not supported yet.
            return;
        }

        foreach (var asType in decorator.AsTypes)
        {
            ApplyDecoratorToServiceType(services, asType, decorator);
        }
    }

    private static void ApplyDecoratorToServiceType(
        IServiceCollection services,
        Type serviceType,
        TypeRegistrationInfo decorator
    )
    {
        var descriptors = new List<(int Index, ServiceDescriptor Descriptor)>();
        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == serviceType)
            {
                descriptors.Add((i, services[i]));
            }
        }

        if (descriptors.Count == 0)
        {
            return;
        }

        foreach (var (index, descriptor) in descriptors)
        {
            var previousDescriptor = descriptor;
            services[index] = DescribeDecoratedDescriptor(
                serviceType,
                previousDescriptor,
                decorator
            );
        }
    }

    private static ServiceDescriptor DescribeDecoratedDescriptor(
        Type serviceType,
        ServiceDescriptor previousDescriptor,
        TypeRegistrationInfo decorator
    )
    {
        var lifetime = previousDescriptor.Lifetime;
        if (!previousDescriptor.IsKeyedService)
        {
            return ServiceDescriptor.Describe(
                serviceType,
                sp =>
                {
                    var inner = CreateFromDescriptor(sp, previousDescriptor);
                    return ActivatorUtilities.CreateInstance(sp, decorator.Type, inner);
                },
                lifetime
            );
        }

        var key = previousDescriptor.ServiceKey;
        return ServiceDescriptor.DescribeKeyed(
            serviceType,
            key!,
            (sp, serviceKey) =>
            {
                var inner = CreateFromDescriptor(sp, previousDescriptor, serviceKey);
                return ActivatorUtilities.CreateInstance(sp, decorator.Type, inner);
            },
            lifetime
        );
    }

    private static object CreateFromDescriptor(
        IServiceProvider provider,
        ServiceDescriptor descriptor,
        object? serviceKey = null
    )
    {
        return descriptor.Lifetime switch
        {
            ServiceLifetime.Singleton => GetOrCreateSingleton(
                provider,
                descriptor,
                () => CreateInstanceUncached(provider, descriptor, serviceKey)
            ),
            ServiceLifetime.Scoped => GetOrCreateScoped(
                provider,
                descriptor,
                () => CreateInstanceUncached(provider, descriptor, serviceKey)
            ),
            _ => CreateInstanceUncached(provider, descriptor, serviceKey),
        };
    }

    private static object CreateInstanceUncached(
        IServiceProvider provider,
        ServiceDescriptor descriptor,
        object? serviceKey
    )
    {
        // Recreate the previous descriptor exactly as DI would have produced it.
        if (descriptor.IsKeyedService)
        {
            if (descriptor.KeyedImplementationInstance is not null)
            {
                return descriptor.KeyedImplementationInstance;
            }

            if (descriptor.KeyedImplementationFactory is not null)
            {
                return descriptor.KeyedImplementationFactory(provider, serviceKey!);
            }

            if (descriptor.KeyedImplementationType is not null)
            {
                return ActivatorUtilities.CreateInstance(
                    provider,
                    descriptor.KeyedImplementationType
                );
            }

            throw new InvalidOperationException(
                $"Unable to create keyed instance for service type '{descriptor.ServiceType}'."
            );
        }

        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Unable to create instance for service type '{descriptor.ServiceType}'."
        );
    }

    private static object GetOrCreateSingleton(
        IServiceProvider provider,
        ServiceDescriptor descriptor,
        Func<object> factory
    )
    {
        var scopeFactory = provider.GetService<IServiceScopeFactory>();
        if (scopeFactory is null)
        {
            return GetOrCreateScoped(provider, descriptor, factory);
        }

        var cache = SingletonCache.GetValue(
            scopeFactory,
            static _ => new ConditionalWeakTable<ServiceDescriptor, object>()
        );
        return GetOrCreateCached(cache, descriptor, factory);
    }

    private static object GetOrCreateScoped(
        IServiceProvider provider,
        ServiceDescriptor descriptor,
        Func<object> factory
    )
    {
        var cache = ScopedCache.GetValue(
            provider,
            static _ => new ConditionalWeakTable<ServiceDescriptor, object>()
        );
        return GetOrCreateCached(cache, descriptor, factory);
    }

    private static object GetOrCreateCached(
        ConditionalWeakTable<ServiceDescriptor, object> cache,
        ServiceDescriptor descriptor,
        Func<object> factory
    )
    {
        if (cache.TryGetValue(descriptor, out var existing))
        {
            return existing;
        }

        var created = factory();
        try
        {
            cache.Add(descriptor, created);
            return created;
        }
        catch (ArgumentException)
        {
            return cache.TryGetValue(descriptor, out var raced) ? raced : created;
        }
    }

    private static ServiceLifetime ConvertLifetime(string lifetime)
    {
        return lifetime switch
        {
            Lifetime.Singleton => ServiceLifetime.Singleton,
            Lifetime.Scoped => ServiceLifetime.Scoped,
            Lifetime.Transient => ServiceLifetime.Transient,
            _ => throw new InvalidOperationException("Unsupported lifetime value."),
        };
    }

    private static void AddKeyedService(
        IServiceCollection services,
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type implementationType,
        object key,
        ServiceLifetime lifetime
    )
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddKeyedSingleton(serviceType, key, implementationType);
                break;
            case ServiceLifetime.Scoped:
                services.AddKeyedScoped(serviceType, key, implementationType);
                break;
            case ServiceLifetime.Transient:
                services.AddKeyedTransient(serviceType, key, implementationType);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(lifetime),
                    lifetime,
                    "Unsupported service lifetime."
                );
        }
    }
}
