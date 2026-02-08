namespace Qudi;

/// <summary>
/// Runtime options for generated AddQudiServices calls.
/// </summary>
public sealed record QudiAddServicesOptions
{
    /// <summary>
    /// The calling assembly name that owns generated registrations.
    /// </summary>
    public string? SelfAssemblyName { get; init; }
}
