namespace Qudi;

/// <summary>
/// Runtime options for generated AddQudiServices calls.
/// </summary>
public readonly record struct QudiAddServicesOptions
{
    /// <summary>
    /// The calling assembly name that owns generated registrations.
    /// </summary>
    public string? SelfAssemblyName { get; init; }
}
