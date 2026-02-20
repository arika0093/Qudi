using System;
using System.Collections.Generic;
using Qudi;
using Qudi.Core.Internal;

namespace Qudi.Container.Core;

/// <summary>
/// Adds base service registrations to a concrete DI container.
/// </summary>
public interface IQudiContainerRegistrationAdapter
{
    /// <summary>
    /// Tries to add one registration request.
    /// </summary>
    bool TryAddService(QudiServiceRegistrationRequest request);
}

/// <summary>
/// Supports layered decorator/composite registration on top of base registrations.
/// </summary>
public interface IQudiLayeredRegistrationAdapter<TDescriptor>
{
    /// <summary>
    /// Whether keyed layers are supported by this container adapter.
    /// </summary>
    bool SupportsKeyedLayers { get; }

    /// <summary>
    /// Gets descriptors currently registered for the given service type.
    /// </summary>
    IReadOnlyList<TDescriptor> GetServiceDescriptors(Type serviceType);

    /// <summary>
    /// Replaces all descriptors registered for the given service type.
    /// </summary>
    void ReplaceServiceDescriptors(Type serviceType, IReadOnlyList<TDescriptor> descriptors);

    /// <summary>
    /// Builds a decorated descriptor that wraps a previous descriptor.
    /// </summary>
    TDescriptor DescribeDecorator(
        Type serviceType,
        TDescriptor previousDescriptor,
        TypeRegistrationInfo decorator
    );

    /// <summary>
    /// Builds a composite descriptor from inner descriptors.
    /// </summary>
    TDescriptor DescribeComposite(
        Type serviceType,
        TypeRegistrationInfo composite,
        IReadOnlyList<TDescriptor> innerDescriptors
    );
}
