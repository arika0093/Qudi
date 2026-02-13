using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qudi.Visualizer;

internal sealed record RegistrationView(
    TypeRegistrationInfo Registration,
    IReadOnlyList<Type> ServiceTypes,
    string ServiceDisplay,
    string ImplementationDisplay,
    string Lifetime,
    string Key,
    string Conditions,
    int Order,
    bool IsDecorator,
    bool IsConditionMatched
);

public sealed record QudiTraceNode(
    string Label,
    string? Detail,
    bool IsMissing,
    bool IsCycle,
    IReadOnlyList<QudiTraceNode> Children
);

public sealed record QudiTraceResult(string Service, IReadOnlyList<QudiTraceNode> Roots);

public sealed record QudiMissingRegistration(string RequiredType, string RequestedBy);

public sealed record QudiCycle(IReadOnlyList<string> Path);

public sealed record QudiLifetimeWarning(string Service, string From, string To, string Message);

public sealed record QudiMultipleRegistration(string Service, string Key, int Count);

public sealed record QudiVisualizationSummary(
    int RegistrationCount,
    int MissingCount,
    int CycleCount,
    int MultipleRegistrationCount,
    int LifetimeWarningCount
);

public sealed record QudiRegistrationTableRow(
    string Service,
    string Implementation,
    string Lifetime,
    string Key,
    string When,
    int Order,
    bool Decorator
);

public sealed record QudiVisualizationReport(
    QudiVisualizationSummary Summary,
    IReadOnlyList<QudiRegistrationTableRow> Registrations,
    IReadOnlyList<QudiMissingRegistration> MissingRegistrations,
    IReadOnlyList<QudiCycle> Cycles,
    IReadOnlyList<QudiMultipleRegistration> MultipleRegistrations,
    IReadOnlyList<QudiLifetimeWarning> LifetimeWarnings,
    IReadOnlyList<QudiTraceResult> Traces,
    [property: JsonIgnore] IReadOnlyList<Type> ExportedTypes
);

public sealed record QudiVisualizationNode(
    string Id,
    string Label,
    string Kind,
    bool IsConditionMatched = true,
    bool IsExternal = false
);

public sealed record QudiVisualizationEdge(
    string From,
    string To,
    string Kind,
    string? Condition = null
);

public sealed record QudiVisualizationGraph(
    IReadOnlyList<QudiVisualizationNode> Nodes,
    IReadOnlyList<QudiVisualizationEdge> Edges
);

internal sealed record VisualizationContext(
    QudiConfiguration Configuration,
    IReadOnlyList<RegistrationView> Applicable,
    IReadOnlyDictionary<Type, List<RegistrationView>> ServiceMap,
    IReadOnlyDictionary<Type, List<RegistrationView>> ImplementationMap
);
