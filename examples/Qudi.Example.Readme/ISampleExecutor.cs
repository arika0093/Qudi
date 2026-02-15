namespace Qudi.Examples;

/// <summary>
/// Represents a sample executor that can run a specific example.
/// </summary>
public interface ISampleExecutor
{
    /// <summary>
    /// Gets the name of the sample.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the sample.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Returns the namespace this sample belongs to.
    /// </summary>
    string Namespace { get; }

    /// <summary>
    /// Executes the sample.
    /// </summary>
    void Execute();
}
