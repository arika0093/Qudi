using System;
using System.Collections.Generic;

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
    bool IsDecorator
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
    IReadOnlyList<QudiTraceResult> Traces
);

internal sealed record VisualizationContext(
    QudiConfiguration Configuration,
    IReadOnlyList<RegistrationView> Applicable,
    IReadOnlyDictionary<Type, List<RegistrationView>> ServiceMap,
    IReadOnlyDictionary<Type, List<RegistrationView>> ImplementationMap
);