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

        try
        {
            File.WriteAllText(dotPath, dot);
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

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();

            var exited = process.WaitForExit(5000);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
                catch
                {
                    // Best-effort cleanup.
                }

                return $"Graphviz 'dot' timed out rendering SVG. Wrote DOT to {dotPath}.";
            }

            if (process.ExitCode != 0)
            {
                return
                    $"Graphviz 'dot' failed to render SVG. Wrote DOT to {dotPath}. {standardError} {standardOutput}";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Graphviz 'dot' was not available. Wrote DOT to {dotPath}. {ex.Message}";
        }
    }
}
