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

            // Read output asynchronously to prevent deadlock
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var exited = process.WaitForExit(5000);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    // Use timeout for cleanup wait to prevent infinite hang
                    process.WaitForExit(1000);
                }
                catch
                {
                    // Best-effort cleanup.
                }

                return $"Graphviz 'dot' timed out rendering SVG. Wrote DOT to {dotPath}.";
            }

            // Wait for async reads to complete after process exits
            var standardOutput = outputTask.Result;
            var standardError = errorTask.Result;

            if (process.ExitCode != 0)
            {
                return $"Graphviz 'dot' failed to render SVG. Wrote DOT to {dotPath}. {standardError} {standardOutput}";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Graphviz 'dot' was not available. Wrote DOT to {dotPath}. {ex.Message}";
        }
    }
}
