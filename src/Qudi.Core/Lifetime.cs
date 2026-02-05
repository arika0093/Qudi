namespace Qudi;

/// <summary>
/// Predefined lifetime keys for Qudi registration.
/// </summary>
public static class Lifetime
{
    /// <summary>
    /// Singleton lifetime.
    /// </summary>
    public const string Singleton = "Singleton";

    /// <summary>
    /// Transient lifetime.
    /// </summary>
    public const string Transient = "Transient";

    /// <summary>
    /// Scoped lifetime.
    /// </summary>
    public const string Scoped = "Scoped";
}
