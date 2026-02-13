using System;
using System.Linq;
using System.Text;

namespace Qudi.Visualizer.OutputWriter;

internal static class DotOutputWriter
{
    public static string Generate(QudiVisualizationGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph Qudi {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box, fontname=\"Arial\"];");

        foreach (var node in graph.Nodes.OrderBy(n => n.Label, StringComparer.Ordinal))
        {
            var shape = node.Kind == "service" ? "ellipse" : "box";
            var style = BuildNodeStyle(node);
            sb.AppendLine(
                $"  \"{EscapeDot(node.Id)}\" [label=\"{EscapeDot(node.Label)}\", shape={shape}{style}];"
            );
        }

        foreach (var edge in graph.Edges)
        {
            var edgeStyle = BuildEdgeStyle(edge);
            sb.AppendLine(
                $"  \"{EscapeDot(edge.From)}\" -> \"{EscapeDot(edge.To)}\"{edgeStyle};"
            );
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildNodeStyle(QudiVisualizationNode node)
    {
        if (!node.IsConditionMatched)
        {
            // 無効化時: 線部分だけ色反映、背景は淡いグレー
            return node.Kind switch
            {
                "interface" => ", style=dashed, fillcolor=\"#f5f5f5\", color=\"#4caf50\"",
                "class" => ", style=dashed, fillcolor=\"#f5f5f5\", color=\"#2196f3\"",
                "decorator" => ", style=dashed, fillcolor=\"#f5f5f5\", color=\"#9c27b0\"",
                _ => ", style=\"filled,dashed\", fillcolor=lightgray, color=gray"
            };
        }

        if (node.IsExternal)
        {
            return ", style=dashed, fillcolor=\"#ffe0b2\", color=\"#ff9800\"";
        }

        return node.Kind switch
        {
            "missing" => ", style=dashed, color=red",
            "interface" => ", style=filled, fillcolor=\"#c8e6c9\"",
            "class" => ", style=filled, fillcolor=\"#bbdefb\"",
            "decorator" => ", style=filled, fillcolor=\"#e1bee7\"",
            _ => string.Empty
        };
    }

    private static string BuildEdgeStyle(QudiVisualizationEdge edge)
    {
        var label = string.IsNullOrWhiteSpace(edge.Condition)
            ? string.Empty
            : $", label=\"{EscapeDot(edge.Condition!)}\"";

        return edge.Kind switch
        {
            "collection" => " [label=\"*\", style=dashed]",
            _ => string.IsNullOrWhiteSpace(label) ? "" : $" [{label.TrimStart(',', ' ')}]"
        };
    }

    private static string EscapeDot(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
