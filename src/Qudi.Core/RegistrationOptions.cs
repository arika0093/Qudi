namespace Qudi;

/// <summary>
/// Specifies how duplicate registrations are handled.
/// </summary>
public enum DuplicateHandling
{
    /// <summary>
    /// Throw an exception when a duplicate registration is found.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Skip the duplicate registration.
    /// </summary>
    Skip = 1,

    /// <summary>
    /// Replace existing registrations with the duplicate registration.
    /// </summary>
    Replace = 2,

    /// <summary>
    /// Add the duplicate registration (default behavior).
    /// </summary>
    Add = 3,
}

/// <summary>
/// Specifies how AsTypes is inferred when omitted.
/// </summary>
public enum AsTypesFallback
{
    /// <summary>
    /// Register only the implementation type itself.
    /// </summary>
    Self = 0,

    /// <summary>
    /// Register all implemented interfaces.
    /// </summary>
    Interfaces = 1,

    /// <summary>
    /// Register the implementation type and all implemented interfaces (default behavior).
    /// </summary>
    SelfWithInterfaces = 2,
}
