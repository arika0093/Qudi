using System;
using System.Collections.Generic;
using Spectre.Console;

namespace Qudi.Visualizer;

internal static class QudiVisualizationRunner
{
    public static void Execute(QudiVisualizationConfiguration configuration)
    {
        var options = configuration.Options.BuildRuntimeOptions();

        if (configuration.Options.ConsoleEncoding != null)
        {
            Console.OutputEncoding = configuration.Options.ConsoleEncoding;
        }

        var report = QudiVisualizationAnalyzer.Analyze(configuration, options);
        var graph = QudiVisualizationGraphBuilder.Build(configuration);
        var warnings = new List<string>();

        // Standard outputs
        if (options.Outputs.Count > 0)
        {
            warnings.AddRange(QudiVisualizationOutputWriter.WriteAll(report, graph, options));
        }

        // Export individual graphs for types with Export=true
        if (!string.IsNullOrEmpty(options.OutputDirectory) && options.OutputFormats.Count > 0)
        {
            warnings.AddRange(ExportIndividualGraphs(configuration, report, options));
        }

        if (options.LoggerFactory != null)
        {
            var logger = options.LoggerFactory.CreateLogger("Qudi.Visualizer");
            QudiVisualizationLoggerRenderer.Render(logger, report);
        }

        if (options.ConsoleOutput != ConsoleDisplay.None)
        {
            var consoleRenderer = new QudiVisualizationConsoleRenderer(AnsiConsole.Console);
            consoleRenderer.Render(
                report,
                warnings,
                options.ConsoleOutput,
                options.SuppressConsolePrompts
            );
        }
    }

    private static List<string> ExportIndividualGraphs(
        QudiConfiguration configuration,
        QudiVisualizationReport report,
        QudiVisualizationRuntimeOptions options
    )
    {
        var warnings = new List<string>();

        if (report.ExportedTypes.Count == 0)
        {
            return warnings;
        }

        // Ensure output directory exists
        try
        {
            System.IO.Directory.CreateDirectory(options.OutputDirectory!);
        }
        catch (Exception ex)
        {
            warnings.Add(
                $"Failed to create output directory '{options.OutputDirectory}': {ex.Message}"
            );
            return warnings;
        }

        foreach (var exportedType in report.ExportedTypes)
        {
            var typeName = QudiVisualizationAnalyzer.ToDisplayName(exportedType);
            var sanitizedTypeName = SanitizeFileName(typeName);
            var subGraph = QudiVisualizationGraphBuilder.BuildFromRoot(configuration, exportedType);

            foreach (var format in options.OutputFormats)
            {
                var fileName = $"{sanitizedTypeName}.{format.ToExtension()}";
                var filePath = System.IO.Path.Combine(options.OutputDirectory!, fileName);

                var fakeOutput = new QudiVisualizationFileOutput(filePath, format);
                var fakeOptions = new QudiVisualizationRuntimeOptions(
                    ConsoleDisplay.None,
                    options.SuppressConsolePrompts,
                    null,
                    [fakeOutput],
                    [],
                    options.GroupByNamespace,
                    options.GraphDirection,
                    options.FontFamily,
                    null,
                    []
                );

                try
                {
                    var outputWarnings = QudiVisualizationOutputWriter.WriteAll(
                        report,
                        subGraph,
                        fakeOptions
                    );
                    warnings.AddRange(outputWarnings);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to export {typeName} to {fileName}: {ex.Message}");
                }
            }
        }

        return warnings;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid));
    }
}
