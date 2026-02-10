namespace Qudi;

/// <summary>
/// Result of strategy processing.
/// </summary>
public readonly struct StrategyResult()
{
    /// <summary>
    /// Whether to use the selected service.
    /// </summary>
    public bool UseService {get;init;}

    /// <summary>
    /// Whether to continue processing further strategies.
    /// </summary>
    public bool Continue {get;init;} = true;

    /// <summary>
    /// Implicit conversion from bool to StrategyResult.
    /// </summary>
    public static implicit operator StrategyResult(bool useService)
    {
        return new StrategyResult
        {
            UseService = useService
        };
    }
}
