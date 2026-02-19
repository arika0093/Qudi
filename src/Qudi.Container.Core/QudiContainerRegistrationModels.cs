using System;
using Qudi;

namespace Qudi.Container.Core;

/// <summary>
/// Target lifetime used by container-independent registration logic.
/// </summary>
public enum QudiContainerLifetime
{
    /// <summary>
    /// Reuse one instance for the whole application.
    /// </summary>
    Singleton,

    /// <summary>
    /// Reuse one instance per scope.
    /// </summary>
    Scoped,

    /// <summary>
    /// Create a new instance each time.
    /// </summary>
    Transient,
}

/// <summary>
/// Registration strategy for service wiring.
/// </summary>
public enum QudiServiceRegistrationKind
{
    /// <summary>
    /// Register service directly to an implementation type.
    /// </summary>
    ImplementationType,

    /// <summary>
    /// Register service as a forwarding alias to an already-registered implementation.
    /// </summary>
    ForwardToImplementation,
}

/// <summary>
/// Container-independent request for adding a single service registration.
/// </summary>
public sealed record QudiServiceRegistrationRequest
{
    /// <summary>
    /// The service type to register.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public required Type ServiceType { get; init; }

    /// <summary>
    /// The implementation type used for service creation.
    /// </summary>
    public required Type ImplementationType { get; init; }

    /// <summary>
    /// Requested service lifetime.
    /// </summary>
    public required QudiContainerLifetime Lifetime { get; init; }

    /// <summary>
    /// Duplicate handling policy.
    /// </summary>
    public required DuplicateHandling DuplicateHandling { get; init; }

    /// <summary>
    /// Registration strategy.
    /// </summary>
    public required QudiServiceRegistrationKind Kind { get; init; }

    /// <summary>
    /// Optional service key for keyed registration.
    /// </summary>
    public object? Key { get; init; }
}
