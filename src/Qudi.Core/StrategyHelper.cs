#pragma warning disable CS9113
using System.Collections.Generic;

namespace Qudi;

/// <summary>
/// Dummy base class for decorator helpers.
/// </summary>
public abstract class StrategyHelper<T>(IEnumerable<T> services)
{
    /// <summary>
    /// Checks whether to use the given service.
    /// </summary>
    protected abstract StrategyResult ShouldUseService(T service);
}

/// <summary>
/// Result of strategy processing.
/// </summary>
public readonly record struct StrategyResult
{
    /// <summary>
    /// Whether to use the selected service.
    /// </summary>
    public bool UseService { get; init; }

    /// <summary>
    /// Whether to continue processing further strategies.
    /// </summary>
    public bool Continue { get; init; }
}
