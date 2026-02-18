using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Qudi;

/// <summary>
/// Metadata for a type registration collected by the source generator.
/// </summary>
public sealed record TypeRegistrationInfo
{
    /// <summary>
    /// The type to register.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type Type { get; init; } = typeof(object);

    /// <summary>
    /// The lifetime of the registration.
    /// </summary>
    public string Lifetime { get; init; } = string.Empty;

    /// <summary>
    /// Required types for this registration.
    /// This information will be used for validation and diagnostics.
    /// </summary>
    public IReadOnlyList<Type> RequiredTypes { get; init; } = [];

    /// <summary>
    /// The types to register as.
    /// It is automatically identified, but you can also specify it explicitly
    /// </summary>
    public IReadOnlyList<Type> AsTypes { get; init; } = [];

    /// <summary>
    /// Make this class accessible from other projects?
    /// </summary>
    public bool UsePublic { get; init; }

    /// <summary>
    /// The key for keyed registrations. If null, no key is used.
    /// </summary>
    public object? Key { get; init; }

    /// <summary>
    /// The order of registration. Higher numbers are registered later.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Trigger registration only in specific conditions.
    /// </summary>
    public IReadOnlyList<string> When { get; init; } = [];

    /// <summary>
    /// Whether this registration is a decorator.
    /// </summary>
    public bool MarkAsDecorator { get; init; }

    /// <summary>
    /// Whether this registration is a composite.
    /// </summary>
    public bool MarkAsComposite { get; init; }

    /// <summary>
    /// Whether this composite should be dispatched by generated code instead of
    /// using the composite inner-service factory.
    /// </summary>
    public bool MarkAsDispatcher { get; init; }

    /// <summary>
    /// Whether to export this type for visualization.
    /// When true, generates a separate dependency graph starting from this type.
    /// </summary>
    public bool Export { get; init; }

    /// <summary>
    /// The namespace where the type is defined.
    /// </summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// The name of the assembly where the type is defined.
    /// </summary>
    public string AssemblyName { get; init; } = string.Empty;
}
