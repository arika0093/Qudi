using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Qudi.Visualizer.OutputWriter;

internal static class MermaidOutputWriter
{
    public static string Generate(QudiVisualizationGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        // Generate node IDs and nodes
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

        // Generate edges
        foreach (var edge in graph.Edges)
        {
            if (!ids.TryGetValue(edge.From, out var fromId) || !ids.TryGetValue(edge.To, out var toId))
            {
                continue;
            }

            var arrow = BuildArrowStyle(edge);
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

        // Add styles for condition-unmatched nodes (grayed out)
        var unmatchedNodes = graph.Nodes.Where(n => !n.IsConditionMatched).ToList();
        if (unmatchedNodes.Count > 0)
        {
            sb.AppendLine("    classDef unmatched fill:#e0e0e0,stroke:#999,stroke-width:1px,color:#666;");
            foreach (var node in unmatchedNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatched;");
                }
            }
        }

        return sb.ToString();
    }

    private static string BuildArrowStyle(QudiVisualizationEdge edge)
    {
        return edge.Kind switch
        {
            "collection" => "-.->|\"*\"|",
            "decorator-provides" => "==>",
            "decorator-wraps" => "-.->",
            _ => "-->"
        };
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
}
