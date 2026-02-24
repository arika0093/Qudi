using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Qudi.Visualizer.OutputWriter;

internal static class MermaidOutputWriter
{
    public static string Generate(
        QudiVisualizationGraph graph,
        bool groupByNamespace = false,
        QudiVisualizationDirection direction = QudiVisualizationDirection.LeftToRight,
        string? fontFamily = null
    )
    {
        var flowDirection = direction == QudiVisualizationDirection.TopToBottom ? "TB" : "LR";
        var normalizedFont = string.IsNullOrWhiteSpace(fontFamily) ? "Consolas" : fontFamily!;
        var escapedFont = EscapeMermaidInitValue(
            normalizedFont
        );
        var sb = new StringBuilder();
        sb.AppendLine($"%%{{init: {{ 'themeVariables': {{ 'fontFamily': '{escapedFont}' }} }} }}%%");
        sb.AppendLine($"flowchart {flowDirection}");

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        // Generate node IDs
        foreach (var nodeId in graph.Nodes.Select(node => node.Id))
        {
            var baseId = SanitizeMermaidId(nodeId);
            var id = baseId;
            var index = 1;
            while (!used.Add(id))
            {
                id = baseId + "_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }
            ids[nodeId] = id;
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
        var nodesById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var collectionPairs = graph
            .Edges.Where(e => e.Kind == "collection")
            .Select(e => (From: e.From, To: e.To))
            .ToHashSet();
        foreach (var edge in graph.Edges)
        {
            if (
                !ids.TryGetValue(edge.From, out var fromId)
                || !ids.TryGetValue(edge.To, out var toId)
            )
            {
                continue;
            }

            if (
                nodesById.TryGetValue(edge.From, out var fromNode)
                && nodesById.TryGetValue(edge.To, out var toNode)
                && fromNode.Kind == "decorator"
                && toNode.Kind == "composite"
                && collectionPairs.Contains((From: edge.To, To: edge.From))
            )
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
            sb.AppendLine(
                "    classDef missing stroke:#c00,stroke-width:2px,stroke-dasharray:5 5;"
            );
            foreach (var node in missingNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} missing;");
                }
            }
        }

        // Add styles for interface nodes
        var interfaceNodes = graph
            .Nodes.Where(n =>
                (n.Kind == "service" || n.Kind == "implementation")
                && n.IsInterface
                && n.IsConditionMatched
                && !n.IsExternal
            )
            .ToList();
        if (interfaceNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef interface fill:#c8e6c9,stroke:#4caf50,stroke-width:2px,color:#000;"
            );
            foreach (var node in interfaceNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} interface;");
                }
            }
        }

        // Add styles for class nodes
        var classNodes = graph
            .Nodes.Where(n =>
                (n.Kind == "service" || n.Kind == "implementation")
                && !n.IsInterface
                && n.IsConditionMatched
                && !n.IsExternal
            )
            .ToList();
        if (classNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef cls fill:#bbdefb,stroke:#2196f3,stroke-width:2px,color:#000;"
            );
            foreach (var node in classNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} cls;");
                }
            }
        }

        // Add styles for decorator nodes
        var decoratorNodes = graph
            .Nodes.Where(n => n.Kind == "decorator" && n.IsConditionMatched && !n.IsExternal)
            .ToList();
        if (decoratorNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef decorator fill:#e1bee7,stroke:#9c27b0,stroke-width:2px,color:#000;"
            );
            foreach (var node in decoratorNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} decorator;");
                }
            }
        }

        // Add styles for composite nodes
        var compositeNodes = graph
            .Nodes.Where(n => n.Kind == "composite" && n.IsConditionMatched && !n.IsExternal)
            .ToList();
        if (compositeNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef composite fill:#f8d7da,stroke:#c62828,stroke-width:2px,color:#000;"
            );
            foreach (var node in compositeNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} composite;");
                }
            }
        }

        // Add styles for dispatcher nodes
        var dispatcherNodes = graph
            .Nodes.Where(n => n.Kind == "dispatcher" && n.IsConditionMatched && !n.IsExternal)
            .ToList();
        if (dispatcherNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef dispatcher fill:#fff2b3,stroke:#f6c445,stroke-width:2px,color:#000;"
            );
            foreach (var node in dispatcherNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} dispatcher;");
                }
            }
        }

        // Add styles for condition-unmatched interface nodes
        var unmatchedInterfaceNodes = graph
            .Nodes.Where(n =>
                (n.Kind == "service" || n.Kind == "implementation")
                && n.IsInterface
                && !n.IsConditionMatched
            )
            .ToList();
        if (unmatchedInterfaceNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef unmatchedInterface fill:#f5f5f5,stroke:#4caf50,stroke-width:1px,stroke-dasharray:3 3,color:#999;"
            );
            foreach (var node in unmatchedInterfaceNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedInterface;");
                }
            }
        }

        // Add styles for condition-unmatched class nodes
        var unmatchedClassNodes = graph
            .Nodes.Where(n =>
                (n.Kind == "service" || n.Kind == "implementation")
                && !n.IsInterface
                && !n.IsConditionMatched
            )
            .ToList();
        if (unmatchedClassNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef unmatchedCls fill:#f5f5f5,stroke:#2196f3,stroke-width:1px,stroke-dasharray:3 3,color:#999;"
            );
            foreach (var node in unmatchedClassNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedCls;");
                }
            }
        }

        // Add styles for condition-unmatched decorator nodes
        var unmatchedDecoratorNodes = graph
            .Nodes.Where(n => n.Kind == "decorator" && !n.IsConditionMatched)
            .ToList();
        if (unmatchedDecoratorNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef unmatchedDecorator fill:#f5f5f5,stroke:#9c27b0,stroke-width:1px,stroke-dasharray:3 3,color:#999;"
            );
            foreach (var node in unmatchedDecoratorNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedDecorator;");
                }
            }
        }

        // Add styles for condition-unmatched composite nodes
        var unmatchedCompositeNodes = graph
            .Nodes.Where(n => n.Kind == "composite" && !n.IsConditionMatched)
            .ToList();
        if (unmatchedCompositeNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef unmatchedComposite fill:#f5f5f5,stroke:#c62828,stroke-width:1px,stroke-dasharray:3 3,color:#999;"
            );
            foreach (var node in unmatchedCompositeNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedComposite;");
                }
            }
        }

        // Add styles for condition-unmatched dispatcher nodes
        var unmatchedDispatcherNodes = graph
            .Nodes.Where(n => n.Kind == "dispatcher" && !n.IsConditionMatched)
            .ToList();
        if (unmatchedDispatcherNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef unmatchedDispatcher fill:#f5f5f5,stroke:#f6c445,stroke-width:1px,stroke-dasharray:3 3,color:#999;"
            );
            foreach (var node in unmatchedDispatcherNodes)
            {
                if (ids.TryGetValue(node.Id, out var id))
                {
                    sb.AppendLine($"    class {id} unmatchedDispatcher;");
                }
            }
        }

        // Add styles for external nodes
        var externalNodes = graph.Nodes.Where(n => n.IsExternal).ToList();
        if (externalNodes.Count > 0)
        {
            sb.AppendLine(
                "    classDef external fill:#ffe0b2,stroke:#ff9800,stroke-width:1px,stroke-dasharray:3 3,color:#000;"
            );
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
        var labelParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(edge.Condition))
        {
            labelParts.Add(edge.Condition!);
        }

        if (!string.IsNullOrWhiteSpace(edge.Key))
        {
            labelParts.Add($"Key:{edge.Key}");
        }

        if (edge.Order != 0)
        {
            labelParts.Add($"Order:{edge.Order}");
        }

        var label =
            labelParts.Count > 0
                ? $"|{EscapeMermaidLabel(string.Join(", ", labelParts))}|"
                : string.Empty;

        return edge.Kind switch
        {
            "collection" => $"-.->|\"*\"|",

            _ => string.IsNullOrWhiteSpace(label) ? "-->" : $"-->{label}",
        };
    }

    private static string EscapeMermaidLabel(string value)
    {
        return value
            .Replace("\"", "\\\"")
            .Replace("<", "#lt;")
            .Replace(">", "#gt;")
            .Replace("|", "#124;");
    }

    private static string EscapeMermaidInitValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
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

    private static void GenerateNodesFlat(
        StringBuilder sb,
        QudiVisualizationGraph graph,
        Dictionary<string, string> ids
    )
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

    private static void GenerateNodesWithSubgraphs(
        StringBuilder sb,
        QudiVisualizationGraph graph,
        Dictionary<string, string> ids
    )
    {
        // Group nodes by namespace
        var externalNodes = new List<QudiVisualizationNode>();
        var namespaceGroups = new Dictionary<string, List<QudiVisualizationNode>>(
            StringComparer.Ordinal
        );

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
