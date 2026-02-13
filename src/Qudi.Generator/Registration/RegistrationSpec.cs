using System;
using System.Collections.Immutable;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Registration;

/// <summary>
/// Specification for a registration to be generated.
/// </summary>
internal sealed record RegistrationSpec
{
    // The type name of the service to register.
    public string TypeName { get; init; } = string.Empty;

    // The namespace where the type is defined.
    public string Namespace { get; init; } = string.Empty;

    // The lifetime of the service (e.g., Transient, Scoped, Singleton).
    public string Lifetime { get; init; } = string.Empty;

    // Conditions under which this registration applies.
    public EquatableArray<string> When { get; init; } = new([]);

    // The types to register the service as.
    public EquatableArray<string> AsTypes { get; init; } = new([]);

    // Required types for this registration.
    public EquatableArray<string> RequiredTypes { get; init; } = new([]);

    // Whether to use public visibility for the registration method.
    public bool UsePublic { get; init; }

    // An optional key literal for keyed registrations.
    public string? KeyLiteral { get; init; }

    // The order of registration.
    public int Order { get; init; }

    // Whether to mark the registration as a decorator.
    public bool MarkAsDecorator { get; init; }

    // Whether to export this type for visualization.
    public bool Export { get; init; }
}
