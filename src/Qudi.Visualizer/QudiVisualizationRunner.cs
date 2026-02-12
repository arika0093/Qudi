using System;
using System.Collections.Generic;
using Spectre.Console;

namespace Qudi.Visualizer;

internal static class QudiVisualizationRunner
{
    public static void Execute(QudiConfiguration configuration, QudiVisualizationRuntimeOptions options)
    {
        if (!options.EnableConsoleOutput && options.Outputs.Count == 0)
        {
            return;
        }

        var report = QudiVisualizationAnalyzer.Analyze(configuration, options);
        var graph = QudiVisualizationGraphBuilder.Build(configuration);
        var warnings = new List<string>();

        if (options.Outputs.Count > 0)
        {
            warnings.AddRange(QudiVisualizationOutputWriter.WriteAll(report, graph, options.Outputs));
        }

        if (options.EnableConsoleOutput)
        {
            var consoleRenderer = new QudiVisualizationConsoleRenderer(AnsiConsole.Console);
            consoleRenderer.Render(report, warnings);
        }
    }
}
