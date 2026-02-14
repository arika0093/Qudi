using System;
using Qudi.Visualizer;

namespace Qudi;

public static class QudiVisualizationExtensions
{
    public static QudiConfigurationRootBuilder EnableVisualizationOutput(
        this QudiConfigurationRootBuilder builder,
        string? filePath = null,
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
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            options.AddOutput(filePath!);
        }

        configure?.Invoke(options);

        builder.AddService(configuration =>
        {
            var runtime = options.BuildRuntimeOptions();
            QudiVisualizationRunner.Execute(configuration, runtime);
        });

        return builder;
    }

    public static QudiConfigurationRootBuilder EnableVisualizationOutput(
        this QudiConfigurationRootBuilder builder,
        Action<QudiVisualizationOptions> configure
    )
    {
        return EnableVisualizationOutput(builder, null, configure);
    }
}
