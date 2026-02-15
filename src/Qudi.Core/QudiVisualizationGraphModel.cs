using System.Collections.Generic;

namespace Qudi;

/// <summary>
/// A node in the dependency graph.
/// </summary>
public sealed record QudiVisualizationNode(
    string Id,
    string Label,
    string Kind,
    bool IsConditionMatched = true,
    bool IsExternal = false,
    bool IsInterface = false
);

/// <summary>
/// An edge in the dependency graph.
/// </summary>
public sealed record QudiVisualizationEdge(
    string From,
    string To,
    string Kind,
    string? Condition = null,
    string? Key = null,
    int Order = 0
);

/// <summary>
/// Dependency graph for registered services.
/// </summary>
public sealed record QudiVisualizationGraph(
    IReadOnlyList<QudiVisualizationNode> Nodes,
    IReadOnlyList<QudiVisualizationEdge> Edges
);
