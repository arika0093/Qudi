using System;
using System.Collections.Generic;

namespace Qudi.Core.Internal;

/// <summary>
/// A normalized registration entry used by downstream services.
/// </summary>
public sealed record QudiRegistrationEntry
{
    /// <summary>
    /// Original registration metadata.
    /// </summary>
    public required TypeRegistrationInfo Registration { get; init; }

    /// <summary>
    /// Effective service types resolved from the registration.
    /// </summary>
    public required IReadOnlyList<Type> ServiceTypes { get; init; }

    /// <summary>
    /// Whether the registration matched current runtime conditions.
    /// </summary>
    public required bool IsConditionMatched { get; init; }

    /// <summary>
    /// Joined condition text for diagnostics and rendering.
    /// </summary>
    public required string? Condition { get; init; }
}

/// <summary>
/// Shared registration graph used by DI registration and visualization.
/// </summary>
public sealed record QudiRegistrationGraph
{
    /// <summary>
    /// All normalized entries including unmatched conditions.
    /// </summary>
    public required IReadOnlyList<QudiRegistrationEntry> AllEntries { get; init; }

    /// <summary>
    /// Entries that match the current conditions.
    /// </summary>
    public required IReadOnlyList<QudiRegistrationEntry> ApplicableEntries { get; init; }

    /// <summary>
    /// Applicable entries after open-generic fallback materialization.
    /// </summary>
    public required IReadOnlyList<QudiRegistrationEntry> MaterializedEntries { get; init; }

    /// <summary>
    /// Registrations applied as base container registrations.
    /// </summary>
    public required IReadOnlyList<TypeRegistrationInfo> BaseRegistrations { get; init; }

    /// <summary>
    /// Registrations applied as layered decorators/composites.
    /// </summary>
    public required IReadOnlyList<TypeRegistrationInfo> LayeredRegistrations { get; init; }

    /// <summary>
    /// Layered entries grouped by service type in application order.
    /// </summary>
    public required IReadOnlyDictionary<
        Type,
        IReadOnlyList<QudiRegistrationEntry>
    > LayersByService { get; init; }

    /// <summary>
    /// Non-decorator implementations grouped by service type.
    /// </summary>
    public required IReadOnlyDictionary<
        Type,
        IReadOnlyList<QudiRegistrationEntry>
    > ImplementationsByService { get; init; }

    /// <summary>
    /// Non-layered implementations grouped by service type.
    /// </summary>
    public required IReadOnlyDictionary<
        Type,
        IReadOnlyList<QudiRegistrationEntry>
    > BaseImplementationsByService { get; init; }

    /// <summary>
    /// Internal assembly names used for external-type determination.
    /// </summary>
    public required IReadOnlyCollection<string> InternalAssemblies { get; init; }

    /// <summary>
    /// Loadable types collected from relevant assemblies.
    /// </summary>
    public required IReadOnlyList<Type> AvailableTypes { get; init; }
}
