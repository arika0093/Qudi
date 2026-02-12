using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
                    File.WriteAllText(output.FilePath, ToJson(report, graph));
                    break;
                case QudiVisualizationFormat.Dot:
                    File.WriteAllText(output.FilePath, ToDot(graph));
                    break;
                case QudiVisualizationFormat.Mermaid:
                    File.WriteAllText(output.FilePath, ToMermaid(graph));
                    break;
                case QudiVisualizationFormat.Dgml:
                    File.WriteAllText(output.FilePath, ToDgml(graph));
                    break;
                case QudiVisualizationFormat.Svg:
                    var warning = TryWriteSvg(output.FilePath, graph);
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

    private static string ToJson(QudiVisualizationReport report, QudiVisualizationGraph graph)
    {
        var payload = new QudiVisualizationPayload(report, graph);
        return JsonSerializer.Serialize(
            payload,
            QudiVisualizationJsonContext.Default.QudiVisualizationPayload
        );
    }

    private static string ToDot(QudiVisualizationGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph Qudi {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box, fontname=\"Arial\"];" );

        foreach (var node in graph.Nodes.OrderBy(n => n.Label, StringComparer.Ordinal))
        {
            var shape = node.Kind == "service" ? "ellipse" : "box";
            var style = node.Kind == "missing" 
                ? ", style=dashed, color=red" 
                : node.Kind == "decorator" 
                    ? ", style=filled, fillcolor=lightblue" 
                    : string.Empty;
            sb.AppendLine(
                $"  \"{EscapeDot(node.Id)}\" [label=\"{EscapeDot(node.Label)}\", shape={shape}{style}];"
            );
        }

        foreach (var edge in graph.Edges)
        {
            var edgeStyle = edge.Kind == "collection" 
                ? " [label=\"*\", style=dashed]"
                : edge.Kind == "decorator-provides"
                    ? " [color=blue]"
                    : edge.Kind == "decorator-wraps"
                        ? " [color=blue, style=dashed]"
                        : "";
            sb.AppendLine(
                $"  \"{EscapeDot(edge.From)}\" -> \"{EscapeDot(edge.To)}\"{edgeStyle};"
            );
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToMermaid(QudiVisualizationGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in graph.Nodes)
        {
            var baseId = SanitizeMermaidId(node.Id);
            var id = baseId;
            var index = 1;
            while (!used.Add(id))
            {
                id = baseId + "_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }
            ids[node.Id] = id;

            sb.AppendLine($"    {id}[\"{EscapeMermaidLabel(node.Label)}\"]");
        }

        foreach (var edge in graph.Edges)
        {
            if (!ids.TryGetValue(edge.From, out var fromId) || !ids.TryGetValue(edge.To, out var toId))
            {
                continue;
            }
            
            // Use different arrow styles for different edge types
            var arrow = edge.Kind switch
            {
                "collection" => "-.->|\"*\"|",  // Dashed arrow with multiplicity label
                "decorator-provides" => "==>",  // Thick arrow for decorator provision
                "decorator-wraps" => "-.->",    // Dashed arrow for decorator wrapping
                _ => "-->"                       // Normal arrow
            };
            
            sb.AppendLine($"    {fromId} {arrow} {toId}");
        }

        // Add styles for missing nodes
        var missingNodes = graph.Nodes.Where(n => n.Kind == "missing").ToList();
        if (missingNodes.Count > 0)
        {
            sb.AppendLine("    classDef missing stroke:#c00,stroke-width:2px,stroke-dasharray:5 5;");
            foreach (var node in missingNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} missing;");
                }
            }
        }

        // Add styles for decorator nodes
        var decoratorNodes = graph.Nodes.Where(n => n.Kind == "decorator").ToList();
        if (decoratorNodes.Count > 0)
        {
            sb.AppendLine("    classDef decorator fill:#add8e6,stroke:#4682b4,stroke-width:2px;");
            foreach (var node in decoratorNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} decorator;");
                }
            }
        }

        return sb.ToString();
    }

    private static string ToDgml(QudiVisualizationGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<DirectedGraph xmlns=\"http://schemas.microsoft.com/vs/2009/dgml\">");
        sb.AppendLine("  <Nodes>");
        foreach (var node in graph.Nodes.OrderBy(n => n.Label, StringComparer.Ordinal))
        {
            sb.AppendLine(
                $"    <Node Id=\"{EscapeXml(node.Id)}\" Label=\"{EscapeXml(node.Label)}\" Category=\"{EscapeXml(node.Kind)}\" />"
            );
        }
        sb.AppendLine("  </Nodes>");
        sb.AppendLine("  <Links>");
        foreach (var edge in graph.Edges)
        {
            sb.AppendLine(
                $"    <Link Source=\"{EscapeXml(edge.From)}\" Target=\"{EscapeXml(edge.To)}\" Category=\"{EscapeXml(edge.Kind)}\" />"
            );
        }
        sb.AppendLine("  </Links>");
        sb.AppendLine("</DirectedGraph>");
        return sb.ToString();
    }

    private static string? TryWriteSvg(string filePath, QudiVisualizationGraph graph)
    {
        var dot = ToDot(graph);
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

    private static string EscapeDot(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeMermaidLabel(string value)
    {
        return value.Replace("\"", "\\\"");
    }

    private static string SanitizeMermaidId(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }
        return sb.Length == 0 ? "node" : sb.ToString();
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

internal sealed record QudiVisualizationPayload(
    QudiVisualizationReport Report,
    QudiVisualizationGraph Graph
);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(QudiVisualizationPayload))]
internal partial class QudiVisualizationJsonContext : JsonSerializerContext;