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

internal sealed record QudiTraceNode(
    string Label,
    string? Detail,
    bool IsMissing,
    bool IsCycle,
    IReadOnlyList<QudiTraceNode> Children
);

internal sealed record QudiTraceResult(string Service, IReadOnlyList<QudiTraceNode> Roots);

internal sealed record QudiMissingRegistration(string RequiredType, string RequestedBy);

internal sealed record QudiCycle(IReadOnlyList<string> Path);

internal sealed record QudiLifetimeWarning(string Service, string From, string To, string Message);

internal sealed record QudiMultipleRegistration(string Service, string Key, int Count);

internal sealed record QudiVisualizationSummary(
    int RegistrationCount,
    int MissingCount,
    int CycleCount,
    int MultipleRegistrationCount,
    int LifetimeWarningCount
);

internal sealed record QudiRegistrationTableRow(
    string Service,
    string Implementation,
    string Lifetime,
    string Key,
    string When,
    int Order,
    bool Decorator
);

internal sealed record QudiVisualizationReport(
    QudiVisualizationSummary Summary,
    IReadOnlyList<QudiRegistrationTableRow> Registrations,
    IReadOnlyList<QudiMissingRegistration> MissingRegistrations,
    IReadOnlyList<QudiCycle> Cycles,
    IReadOnlyList<QudiMultipleRegistration> MultipleRegistrations,
    IReadOnlyList<QudiLifetimeWarning> LifetimeWarnings,
    IReadOnlyList<QudiTraceResult> Traces,
    [property: JsonIgnore] IReadOnlyList<Type> ExportedTypes
);

internal sealed record QudiVisualizationNode(
    string Id,
    string Label,
    string Kind,
    bool IsConditionMatched = true,
    bool IsExternal = false,
    bool IsInterface = false
);

internal sealed record QudiVisualizationEdge(
    string From,
    string To,
    string Kind,
    string? Condition = null,
    string? Key = null,
    int Order = 0
);

internal sealed record QudiVisualizationGraph(
    IReadOnlyList<QudiVisualizationNode> Nodes,
    IReadOnlyList<QudiVisualizationEdge> Edges
);

internal sealed record VisualizationContext(
    QudiConfiguration Configuration,
    IReadOnlyList<RegistrationView> Applicable,
    IReadOnlyDictionary<Type, List<RegistrationView>> ServiceMap,
    IReadOnlyDictionary<Type, List<RegistrationView>> ImplementationMap
);
