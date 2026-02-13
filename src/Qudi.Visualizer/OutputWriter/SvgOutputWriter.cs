using System;
using System.Diagnostics;
using System.IO;

namespace Qudi.Visualizer.OutputWriter;

internal static class SvgOutputWriter
{
    public static string? TryWrite(string filePath, QudiVisualizationGraph graph)
    {
        var dot = DotOutputWriter.Generate(graph);
        var dotPath = Path.ChangeExtension(filePath, ".dot");
        File.WriteAllText(dotPath, dot);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dot",
                Arguments = $"-Tsvg \"{dotPath}\" -o \"{filePath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return $"Unable to start 'dot' to render SVG. Wrote DOT to {dotPath}.";
            }

            process.WaitForExit(5000);
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                return $"Graphviz 'dot' failed to render SVG. Wrote DOT to {dotPath}. {error}";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Graphviz 'dot' was not available. Wrote DOT to {dotPath}. {ex.Message}";
        }
    }
}
