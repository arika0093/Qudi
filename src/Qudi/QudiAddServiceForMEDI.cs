using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Qudi;

/// <summary>
/// Provides extension methods for registering services in Qudi.
/// </summary>
public static class QudiAddServiceForMicrosoftExtensionsDependencyInjection
{
    /// <summary>
    /// Registers Qudi-collected service definitions into <paramref name="services" />.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="types">Collected registrations from source generation.</param>
    /// <param name="configuration">Runtime registration configuration.</param>
    /// <param name="options">
    /// Additional options generated at compile time.
    /// Used to honor <see cref="QudiConfiguration.UseSelfImplementsOnly" />.
    /// </param>
    public static IServiceCollection AddQudiServices(
        IServiceCollection services,
        IReadOnlyList<TypeRegistrationInfo> types,
        QudiConfiguration configuration,
        QudiAddServicesOptions? options = null
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

        var applicable = types
            .Where(t => ShouldRegister(t, configuration, options))
            .OrderBy(t => t.Order)
            .ToList();

        var registrations = applicable.Where(t => !t.MarkAsDecorator).ToList();
        var decorators = applicable.Where(t => t.MarkAsDecorator).ToList();

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
        QudiConfiguration configuration,
        QudiAddServicesOptions? options
    )
    {
        if (configuration.UseSelfImplementsOnlyEnabled)
        {
            return string.Equals(
                registration.AssemblyName,
                options?.SelfAssemblyName,
                StringComparison.Ordinal
            );
        }

        if (registration.When.Count == 0)
        {
            return true;
        }

        return registration.When.Any(configuration.Conditions.Contains);
    }

    private static void RegisterService(
        IServiceCollection services,
        TypeRegistrationInfo registration
    )
    {
        // Register implementation first, then map interfaces to the same instance path.
        var lifetime = ConvertLifetime(registration.Lifetime);

        if (registration.Key is null)
        {
            services.Add(new ServiceDescriptor(registration.Type, registration.Type, lifetime));

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
        // Decorators wrap the latest registration for each service type.
        if (decorator.AsTypes.Count == 0)
        {
            return;
        }

        if (decorator.Key is not null)
        {
            // Keyed decorators are not supported yet.
            return;
        }

        var lifetime = ConvertLifetime(decorator.Lifetime);

        foreach (var asType in decorator.AsTypes)
        {
            var lastDescriptor = FindLastDescriptor(services, asType);
            if (lastDescriptor is null)
            {
                continue;
            }

            services.Remove(lastDescriptor);
            services.Add(
                ServiceDescriptor.Describe(
                    asType,
                    sp =>
                    {
                        var inner = CreateFromDescriptor(sp, lastDescriptor);
                        return ActivatorUtilities.CreateInstance(sp, decorator.Type, inner);
                    },
                    lifetime
                )
            );
        }
    }

    private static ServiceDescriptor? FindLastDescriptor(
        IServiceCollection services,
        Type serviceType
    )
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == serviceType)
            {
                return descriptor;
            }
        }

        return null;
    }

    private static object CreateFromDescriptor(
        IServiceProvider provider,
        ServiceDescriptor descriptor
    )
    {
        // Recreate the previous descriptor exactly as DI would have produced it.
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
