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
    /// <param name="types">Collected registrations from source generation.</param>
    /// <param name="configuration">Runtime registration configuration.</param>
    public static IServiceCollection AddQudiServices(
        IServiceCollection services,
        IReadOnlyList<TypeRegistrationInfo> types,
        QudiConfiguration configuration
    )
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (types is null)
        {
            throw new ArgumentNullException(nameof(types));
        }

        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // TODO: The logic here needs to be clearly refactored.
        var applicable = types
            .Where(t => ShouldRegister(t, configuration.Conditions))
            .OrderBy(t => IsOpenGenericRegistration(t) ? 0 : 1)
            .ThenBy(t => t.Order)
            .ToList();

        var registrations = applicable.Where(t => !t.MarkAsDecorator).ToList();
        var decorators = applicable.Where(t => t.MarkAsDecorator).OrderBy(t => t.Order).ToList();

        foreach (var registration in registrations)
        {
            RegisterService(services, registration);
        }

        foreach (var decorator in decorators)
        {
            ApplyDecorator(services, decorator);
        }

        return services;
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

        if (registration.AsTypes.Count == 0)
        {
            services.Add(new ServiceDescriptor(registration.Type, registration.Type, lifetime));
            return;
        }

        if (registration.Key is null)
        {
            services.Add(new ServiceDescriptor(registration.Type, registration.Type, lifetime));

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
