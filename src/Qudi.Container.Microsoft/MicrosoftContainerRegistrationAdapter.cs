using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Qudi.Container.Core;

namespace Qudi.Container.Microsoft;

// Internal adapter that applies Qudi registrations to Microsoft DI container.
internal sealed class MicrosoftContainerRegistrationAdapter(IServiceCollection services)
    : IQudiContainerRegistrationAdapter,
        IQudiLayeredRegistrationAdapter<ServiceDescriptor>
{
    private static readonly ConditionalWeakTable<
        IServiceScopeFactory,
        ConditionalWeakTable<ServiceDescriptor, object>
    > SingletonCache = new();

    private static readonly ConditionalWeakTable<
        IServiceProvider,
        ConditionalWeakTable<ServiceDescriptor, object>
    > ScopedCache = new();

    private readonly IServiceCollection _services = services ?? throw new ArgumentNullException(nameof(services));

    public bool SupportsKeyedLayers => false;

    public bool TryAddService(QudiServiceRegistrationRequest request)
    {
        if (!ShouldAddDescriptor(request.ServiceType, request.Key, request.DuplicateHandling))
        {
            return false;
        }

        var lifetime = ConvertLifetime(request.Lifetime);

        if (request.Key is null)
        {
            ServiceDescriptor descriptor = request.Kind switch
            {
                QudiServiceRegistrationKind.ImplementationType => new ServiceDescriptor(
                    request.ServiceType,
                    request.ImplementationType,
                    lifetime
                ),
                QudiServiceRegistrationKind.ForwardToImplementation =>
                    ServiceDescriptor.Describe(
                        request.ServiceType,
                        sp => sp.GetRequiredService(request.ImplementationType),
                        lifetime
                    ),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.Kind,
                    "Unsupported registration kind."
                ),
            };

            _services.Add(descriptor);
            return true;
        }

        if (request.Kind == QudiServiceRegistrationKind.ForwardToImplementation)
        {
            throw new InvalidOperationException(
                "Keyed forwarding registrations are not supported."
            );
        }

        AddKeyedService(
            request.ServiceType,
            request.ImplementationType,
            request.Key,
            lifetime
        );
        return true;
    }

    public IReadOnlyList<ServiceDescriptor> GetServiceDescriptors(Type serviceType)
    {
        var descriptors = new List<ServiceDescriptor>();
        for (var i = 0; i < _services.Count; i++)
        {
            if (_services[i].ServiceType == serviceType)
            {
                descriptors.Add(_services[i]);
            }
        }

        return descriptors;
    }

    public void ReplaceServiceDescriptors(
        Type serviceType,
        IReadOnlyList<ServiceDescriptor> descriptors
    )
    {
        var indexes = FindDescriptorIndexes(serviceType, key: null, includeKeyed: true);
        for (var i = indexes.Count - 1; i >= 0; i--)
        {
            _services.RemoveAt(indexes[i]);
        }

        foreach (var descriptor in descriptors)
        {
            _services.Add(descriptor);
        }
    }

    ServiceDescriptor IQudiLayeredRegistrationAdapter<ServiceDescriptor>.DescribeDecorator(
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

    ServiceDescriptor IQudiLayeredRegistrationAdapter<ServiceDescriptor>.DescribeComposite(
        Type serviceType,
        TypeRegistrationInfo composite,
        IReadOnlyList<ServiceDescriptor> innerDescriptors
    )
    {
        var compositeLifetime = GetCompositeLifetime(innerDescriptors);

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
            compositeLifetime
        );
    }

    private static ServiceLifetime GetCompositeLifetime(
        IReadOnlyList<ServiceDescriptor> innerDescriptors
    )
    {
        if (innerDescriptors.Any(d => d.Lifetime == ServiceLifetime.Singleton))
        {
            return ServiceLifetime.Singleton;
        }

        if (innerDescriptors.Any(d => d.Lifetime == ServiceLifetime.Scoped))
        {
            return ServiceLifetime.Scoped;
        }

        return ServiceLifetime.Transient;
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
                return ActivatorUtilities.CreateInstance(provider, descriptor.KeyedImplementationType);
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

    private bool ShouldAddDescriptor(Type serviceType, object? key, DuplicateHandling duplicate)
    {
        var matches = FindDescriptorIndexes(serviceType, key, includeKeyed: false);
        if (matches.Count == 0)
        {
            return true;
        }

        switch (duplicate)
        {
            case DuplicateHandling.Skip:
                return false;
            case DuplicateHandling.Throw:
                throw new InvalidOperationException(
                    $"Duplicate registration detected for '{serviceType}'."
                );
            case DuplicateHandling.Replace:
                for (var i = matches.Count - 1; i >= 0; i--)
                {
                    _services.RemoveAt(matches[i]);
                }
                return true;
            case DuplicateHandling.Add:
            default:
                return true;
        }
    }

    private List<int> FindDescriptorIndexes(Type serviceType, object? key, bool includeKeyed)
    {
        var indexes = new List<int>();
        for (var i = 0; i < _services.Count; i++)
        {
            var descriptor = _services[i];
            if (descriptor.ServiceType != serviceType)
            {
                continue;
            }

            if (includeKeyed)
            {
                indexes.Add(i);
                continue;
            }

            if (key is null)
            {
                if (!descriptor.IsKeyedService)
                {
                    indexes.Add(i);
                }
                continue;
            }

            if (!descriptor.IsKeyedService)
            {
                continue;
            }

            if (Equals(descriptor.ServiceKey, key))
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    private void AddKeyedService(
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
                _services.AddKeyedSingleton(serviceType, key, implementationType);
                break;
            case ServiceLifetime.Scoped:
                _services.AddKeyedScoped(serviceType, key, implementationType);
                break;
            case ServiceLifetime.Transient:
                _services.AddKeyedTransient(serviceType, key, implementationType);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(lifetime),
                    lifetime,
                    "Unsupported service lifetime."
                );
        }
    }

    private static ServiceLifetime ConvertLifetime(string lifetime)
    {
        return lifetime switch
        {
            Lifetime.Singleton => ServiceLifetime.Singleton,
            Lifetime.Scoped => ServiceLifetime.Scoped,
            Lifetime.Transient => ServiceLifetime.Transient,
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "Unsupported service lifetime."
            ),
        };
    }
}
