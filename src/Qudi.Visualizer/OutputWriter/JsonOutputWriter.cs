using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Qudi.Visualizer.OutputWriter;

internal static class JsonOutputWriter
{
    public static void Write(
        string filePath,
        QudiVisualizationReport report,
        QudiVisualizationGraph graph
    )
    {
        var payload = new QudiVisualizationPayload(report, graph);
        var json = JsonSerializer.Serialize(
            payload,
            QudiVisualizationJsonContext.Default.QudiVisualizationPayload
        );
        File.WriteAllText(filePath, json);
    }
}
