using System;
using System.Collections.Generic;
using Qudi.Visualizer;

namespace Qudi;

/// <summary>
/// Configuration for Qudi visualization execution.
/// </summary>
public sealed record QudiVisualizationConfiguration : QudiConfiguration
{
    /// <summary>
    /// Visualization options.
    /// </summary>
    public required QudiVisualizationOptions Options { get; init; }
}

/// <summary>
/// Builder for Qudi visualization configuration.
/// </summary>
public sealed class QudiVisualizationConfigurationBuilder
    : QudiConfigurationBuilder<QudiVisualizationConfiguration>
{
    private readonly QudiVisualizationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="QudiVisualizationConfigurationBuilder"/> class.
    /// </summary>
    /// <param name="options">Visualization options.</param>
    public QudiVisualizationConfigurationBuilder(QudiVisualizationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    protected override QudiVisualizationConfiguration CreateTypedConfiguration(
        IReadOnlyCollection<TypeRegistrationInfo> registrations,
        IReadOnlyCollection<string> conditions
    ) =>
        new()
        {
            Registrations = registrations,
            Conditions = conditions,
            Options = _options,
        };

    /// <inheritdoc />
    protected override void ExecuteTyped(QudiVisualizationConfiguration configuration)
    {
        QudiVisualizationRunner.Execute(configuration);
    }
}
