using System;
using System.Diagnostics.CodeAnalysis;
using Qudi;

namespace Qudi.Container.Core;

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
    public required Type ServiceType { get; init; }

    /// <summary>
    /// The implementation type used for service creation.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public required Type ImplementationType { get; init; }

    /// <summary>
    /// Requested service lifetime.
    /// </summary>
    public required string Lifetime { get; init; }

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
