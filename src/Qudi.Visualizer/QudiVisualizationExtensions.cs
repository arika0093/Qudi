using System;
using Qudi.Visualizer;

namespace Qudi;

/// <summary>
/// Extension methods for Qudi visualization.
/// </summary>
public static class QudiVisualizationExtensions
{
    /// <summary>
    /// Enables the Qudi visualization output. This will run the visualization runner with the specified options.
    /// </summary>
    /// <param name="builder"> The Qudi configuration root builder to add the visualization output to.</param>
    /// <param name="configure"> An action to configure the visualization options.</param>
    public static QudiConfigurationRootBuilder EnableVisualizationOutput(
        this QudiConfigurationRootBuilder builder,
        Action<QudiVisualizationOptions>? configure = null
    )
    {
        // export UTF-8
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var options = new QudiVisualizationOptions();
        configure?.Invoke(options);

        builder.AddService(configuration =>
        {
            var runtime = options.BuildRuntimeOptions();
            QudiVisualizationRunner.Execute(configuration, runtime);
        });

        return builder;
    }
}
