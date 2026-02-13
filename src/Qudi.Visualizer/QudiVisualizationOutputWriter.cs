using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qudi.Visualizer.OutputWriter;

namespace Qudi.Visualizer;

internal static class QudiVisualizationOutputWriter
{
    public static List<string> WriteAll(
        QudiVisualizationReport report,
        QudiVisualizationGraph graph,
        IReadOnlyCollection<QudiVisualizationFileOutput> outputs
    )
    {
        var warnings = new List<string>();

        foreach (var output in outputs)
        {
            EnsureDirectory(output.FilePath);

            switch (output.Format)
            {
                case QudiVisualizationFormat.Json:
                    JsonOutputWriter.Write(output.FilePath, report, graph);
                    break;
                case QudiVisualizationFormat.Dot:
                    File.WriteAllText(output.FilePath, DotOutputWriter.Generate(graph));
                    break;
                case QudiVisualizationFormat.Mermaid:
                    File.WriteAllText(output.FilePath, MermaidOutputWriter.Generate(graph));
                    break;
                case QudiVisualizationFormat.Svg:
                    var warning = SvgOutputWriter.TryWrite(output.FilePath, graph);
                    if (!string.IsNullOrWhiteSpace(warning))
                    {
                        warnings.Add(warning!);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unsupported visualization format.");
            }
        }

        return warnings;
    }

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}

internal sealed record QudiVisualizationPayload(
    QudiVisualizationReport Report,
    QudiVisualizationGraph Graph
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(QudiVisualizationPayload))]
internal partial class QudiVisualizationJsonContext : JsonSerializerContext;
