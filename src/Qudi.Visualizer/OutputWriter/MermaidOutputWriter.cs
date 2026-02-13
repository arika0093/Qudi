using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Qudi.Visualizer.OutputWriter;

internal static class MermaidOutputWriter
{
    public static string Generate(QudiVisualizationGraph graph, bool groupByNamespace = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        // Generate node IDs
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
        }

        // Generate nodes (with or without subgraphs)
        if (groupByNamespace)
        {
            GenerateNodesWithSubgraphs(sb, graph, ids);
        }
        else
        {
            GenerateNodesFlat(sb, graph, ids);
        }

        // Generate edges with condition labels
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

        // Add styles for interface nodes (薄い緑色背景)
        var interfaceNodes = graph.Nodes.Where(n => n.Kind == "interface" && n.IsConditionMatched && !n.IsExternal).ToList();
        if (interfaceNodes.Count > 0)
        {
            sb.AppendLine("    classDef interface fill:#c8e6c9,stroke:#4caf50,stroke-width:2px,color:#000;");
            foreach (var node in interfaceNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} interface;");
                }
            }
        }

        // Add styles for class nodes (薄い青色背景)
        var classNodes = graph.Nodes.Where(n => n.Kind == "class" && n.IsConditionMatched && !n.IsExternal).ToList();
        if (classNodes.Count > 0)
        {
            sb.AppendLine("    classDef cls fill:#bbdefb,stroke:#2196f3,stroke-width:2px,color:#000;");
            foreach (var node in classNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} cls;");
                }
            }
        }

        // Add styles for decorator nodes (紫系、文字色は黒)
        var decoratorNodes = graph.Nodes.Where(n => n.Kind == "decorator" && n.IsConditionMatched && !n.IsExternal).ToList();
        if (decoratorNodes.Count > 0)
        {
            sb.AppendLine("    classDef decorator fill:#e1bee7,stroke:#9c27b0,stroke-width:2px,color:#000;");
            foreach (var node in decoratorNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} decorator;");
                }
            }
        }

        // Add styles for condition-unmatched interface nodes (線だけ緑、背景は淡いグレー)
        var unmatchedInterfaceNodes = graph.Nodes.Where(n => n.Kind == "interface" && !n.IsConditionMatched).ToList();
        if (unmatchedInterfaceNodes.Count > 0)
        {
            sb.AppendLine("    classDef unmatchedInterface fill:#f5f5f5,stroke:#4caf50,stroke-width:1px,stroke-dasharray:3 3,color:#999;");
            foreach (var node in unmatchedInterfaceNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedInterface;");
                }
            }
        }

        // Add styles for condition-unmatched class nodes (線だけ青、背景は淡いグレー)
        var unmatchedClassNodes = graph.Nodes.Where(n => n.Kind == "class" && !n.IsConditionMatched).ToList();
        if (unmatchedClassNodes.Count > 0)
        {
            sb.AppendLine("    classDef unmatchedCls fill:#f5f5f5,stroke:#2196f3,stroke-width:1px,stroke-dasharray:3 3,color:#999;");
            foreach (var node in unmatchedClassNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedCls;");
                }
            }
        }

        // Add styles for condition-unmatched decorator nodes (線だけ紫、背景は淡いグレー)
        var unmatchedDecoratorNodes = graph.Nodes.Where(n => n.Kind == "decorator" && !n.IsConditionMatched).ToList();
        if (unmatchedDecoratorNodes.Count > 0)
        {
            sb.AppendLine("    classDef unmatchedDecorator fill:#f5f5f5,stroke:#9c27b0,stroke-width:1px,stroke-dasharray:3 3,color:#999;");
            foreach (var node in unmatchedDecoratorNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedDecorator;");
                }
            }
        }

        // Add styles for external nodes (淡いオレンジベース)
        var externalNodes = graph.Nodes.Where(n => n.IsExternal).ToList();
        if (externalNodes.Count > 0)
        {
            sb.AppendLine("    classDef external fill:#ffe0b2,stroke:#ff9800,stroke-width:1px,stroke-dasharray:3 3,color:#e65100;");
            foreach (var node in externalNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} external;");
                }
            }
        }

        return sb.ToString();
    }

    private static string BuildArrowStyle(QudiVisualizationEdge edge)
    {
        var label = string.IsNullOrWhiteSpace(edge.Condition) 
            ? string.Empty 
            : $"|{EscapeMermaidLabel(edge.Condition!)}|";

        return edge.Kind switch
        {
            "collection" => $"-.->|\"*\"|",

            _ => string.IsNullOrWhiteSpace(label) ? "-->" : $"-->{label}"
        };
    }

    private static string EscapeMermaidLabel(string value)
    {
        return value
            .Replace("\"", "\\\"")
            .Replace("<", "#lt;")
            .Replace(">", "#gt;");
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

    private static void GenerateNodesFlat(StringBuilder sb, QudiVisualizationGraph graph, Dictionary<string, string> ids)
    {
        foreach (var node in graph.Nodes)
        {
            if (ids.TryGetValue(node.Id, out var id))
            {
                var escapedLabel = EscapeMermaidLabel(node.Label);
                sb.AppendLine($"    {id}[\"{escapedLabel}\"]");
            }
        }
    }

    private static void GenerateNodesWithSubgraphs(StringBuilder sb, QudiVisualizationGraph graph, Dictionary<string, string> ids)
    {
        // Group nodes by namespace
        var externalNodes = new List<QudiVisualizationNode>();
        var namespaceGroups = new Dictionary<string, List<QudiVisualizationNode>>(StringComparer.Ordinal);

        foreach (var node in graph.Nodes)
        {
            if (node.IsExternal)
            {
                externalNodes.Add(node);
            }
            else
            {
                var ns = GetNamespace(node.Id);
                if (!namespaceGroups.TryGetValue(ns, out var group))
                {
                    group = new List<QudiVisualizationNode>();
                    namespaceGroups[ns] = group;
                }
                group.Add(node);
            }
        }

        // Write external subgraph
        if (externalNodes.Count > 0)
        {
            sb.AppendLine("    subgraph External");
            foreach (var node in externalNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    var escapedLabel = EscapeMermaidLabel(node.Label);
                    sb.AppendLine($"        {id}[\"{escapedLabel}\"]");
                }
            }
            sb.AppendLine("    end");
        }

        // Write namespace subgraphs
        foreach (var kvp in namespaceGroups.OrderBy(x => x.Key))
        {
            var ns = kvp.Key;
            var nodes = kvp.Value;
            var subgraphId = SanitizeMermaidId(ns);
            
            sb.AppendLine($"    subgraph {subgraphId} [\"{ns}\"]");
            foreach (var node in nodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    var escapedLabel = EscapeMermaidLabel(node.Label);
                    sb.AppendLine($"        {id}[\"{escapedLabel}\"]");
                }
            }
            sb.AppendLine("    end");
        }
    }

    private static string GetNamespace(string fullTypeName)
    {
        var lastDotIndex = fullTypeName.LastIndexOf('.');
        if (lastDotIndex < 0)
        {
            return "(global)";
        }
        return fullTypeName.Substring(0, lastDotIndex);
    }
}
