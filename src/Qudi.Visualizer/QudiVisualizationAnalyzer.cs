using System;
using System.Collections.Generic;
using System.Linq;

namespace Qudi.Visualizer;

internal static class QudiVisualizationAnalyzer
{
    public static QudiVisualizationReport Analyze(
        QudiConfiguration configuration,
        QudiVisualizationRuntimeOptions runtimeOptions
    )
    {
        var context = BuildContext(configuration);
        var missing = DetectMissing(context);
        var cycles = DetectCycles(context);
        var multiples = DetectMultipleRegistrations(context);
        var lifetimeWarnings = DetectLifetimeWarnings(context);
        var traces = BuildTraces(context, runtimeOptions.TraceServices);

        var rows = context
            .Applicable.SelectMany(registration =>
                registration.ServiceTypes.Select(service => new QudiRegistrationTableRow(
                    ToDisplayName(service),
                    registration.ImplementationDisplay,
                    registration.Lifetime,
                    registration.Key,
                    registration.Conditions,
                    registration.Order,
                    registration.IsDecorator
                ))
            )
            .OrderBy(r => r.Service, StringComparer.Ordinal)
            .ThenBy(r => r.Order)
            .ThenBy(r => r.Implementation, StringComparer.Ordinal)
            .ToList();

        var summary = new QudiVisualizationSummary(
            rows.Count,
            missing.Count,
            cycles.Count,
            multiples.Count,
            lifetimeWarnings.Count
        );

        return new QudiVisualizationReport(
            summary,
            rows,
            missing,
            cycles,
            multiples,
            lifetimeWarnings,
            traces
        );
    }

    private static VisualizationContext BuildContext(QudiConfiguration configuration)
    {
        var applicable = configuration
            .Registrations.Where(r => IsApplicable(r, configuration.Conditions))
            .Select(registration =>
            {
                var serviceTypes = registration.AsTypes.Count > 0
                    ? registration.AsTypes.Distinct().ToList()
                    : [registration.Type];

                return new RegistrationView(
                    registration,
                    serviceTypes,
                    string.Join(", ", serviceTypes.Select(ToDisplayName)),
                    ToDisplayName(registration.Type),
                    registration.Lifetime,
                    registration.Key?.ToString() ?? "-",
                    registration.When.Count == 0 ? "*" : string.Join(", ", registration.When),
                    registration.Order,
                    registration.MarkAsDecorator
                );
            })
            .OrderBy(r => r.Order)
            .ThenBy(r => r.ImplementationDisplay, StringComparer.Ordinal)
            .ToList();

        var serviceMap = new Dictionary<Type, List<RegistrationView>>();
        var implementationMap = new Dictionary<Type, List<RegistrationView>>();

        foreach (var registration in applicable)
        {
            foreach (var serviceType in registration.ServiceTypes)
            {
                if (!serviceMap.TryGetValue(serviceType, out var list))
                {
                    list = [];
                    serviceMap[serviceType] = list;
                }

                list.Add(registration);
            }

            if (!implementationMap.TryGetValue(registration.Registration.Type, out var implList))
            {
                implList = [];
                implementationMap[registration.Registration.Type] = implList;
            }

            implList.Add(registration);
        }

        return new VisualizationContext(configuration, applicable, serviceMap, implementationMap);
    }

    private static List<QudiMissingRegistration> DetectMissing(VisualizationContext context)
    {
        var result = new List<QudiMissingRegistration>();
        var serviceAndSelf = new HashSet<Type>();

        foreach (var registration in context.Applicable)
        {
            serviceAndSelf.Add(registration.Registration.Type);
            foreach (var serviceType in registration.ServiceTypes)
            {
                serviceAndSelf.Add(serviceType);
            }
        }

        foreach (var registration in context.Applicable)
        {
            foreach (var required in registration.Registration.RequiredTypes.Distinct())
            {
                if (serviceAndSelf.Contains(required))
                {
                    continue;
                }

                // Skip external types (e.g., Microsoft.Extensions.*, System.*)
                if (IsExternalType(required))
                {
                    continue;
                }

                result.Add(
                    new QudiMissingRegistration(
                        ToDisplayName(required),
                        registration.ImplementationDisplay
                    )
                );
            }
        }

        return result
            .DistinctBy(r => $"{r.RequiredType}|{r.RequestedBy}")
            .OrderBy(r => r.RequiredType, StringComparer.Ordinal)
            .ThenBy(r => r.RequestedBy, StringComparer.Ordinal)
            .ToList();
    }

    private static List<QudiCycle> DetectCycles(VisualizationContext context)
    {
        var edges = BuildImplementationEdges(context);
        var visited = new HashSet<Type>();
        var onStack = new HashSet<Type>();
        var stack = new Stack<Type>();
        var cycles = new List<QudiCycle>();
        var seenCycleKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in edges.Keys)
        {
            Dfs(node);
        }

        return cycles;

        void Dfs(Type current)
        {
            if (visited.Contains(current))
            {
                return;
            }

            visited.Add(current);
            onStack.Add(current);
            stack.Push(current);

            if (edges.TryGetValue(current, out var nextNodes))
            {
                foreach (var next in nextNodes)
                {
                    if (!visited.Contains(next))
                    {
                        Dfs(next);
                    }
                    else if (onStack.Contains(next))
                    {
                        var path = new List<Type>();
                        foreach (var item in stack)
                        {
                            path.Add(item);
                            if (item == next)
                            {
                                break;
                            }
                        }

                        path.Reverse();
                        path.Add(next);

                        var key = string.Join("->", path.Select(t => t.FullName));
                        if (seenCycleKeys.Add(key))
                        {
                            cycles.Add(new QudiCycle(path.Select(ToDisplayName).ToList()));
                        }
                    }
                }
            }

            stack.Pop();
            onStack.Remove(current);
        }
    }

    private static Dictionary<Type, HashSet<Type>> BuildImplementationEdges(VisualizationContext context)
    {
        var edges = new Dictionary<Type, HashSet<Type>>();

        foreach (var registration in context.Applicable.Where(r => !r.IsDecorator))
        {
            var implType = registration.Registration.Type;
            if (!edges.TryGetValue(implType, out var targets))
            {
                targets = [];
                edges[implType] = targets;
            }

            foreach (var required in registration.Registration.RequiredTypes.Distinct())
            {
                var candidates = ResolveImplementationCandidates(context, required);
                foreach (var target in candidates)
                {
                    targets.Add(target.Registration.Type);
                }
            }
        }

        return edges;
    }

    private static List<QudiMultipleRegistration> DetectMultipleRegistrations(VisualizationContext context)
    {
        return context
            .Applicable.Where(r => !r.IsDecorator)
            .SelectMany(r => r.ServiceTypes.Select(service => (Service: service, Key: r.Key)))
            .GroupBy(x => (x.Service, x.Key), x => x)
            .Where(g => g.Count() > 1)
            .Select(g =>
                new QudiMultipleRegistration(
                    ToDisplayName(g.Key.Service),
                    string.IsNullOrWhiteSpace(g.Key.Key) ? "-" : g.Key.Key,
                    g.Count()
                )
            )
            .OrderBy(x => x.Service, StringComparer.Ordinal)
            .ToList();
    }

    private static List<QudiLifetimeWarning> DetectLifetimeWarnings(VisualizationContext context)
    {
        var warnings = new List<QudiLifetimeWarning>();

        foreach (var registration in context.Applicable.Where(r => !r.IsDecorator))
        {
            var fromLifetime = NormalizeLifetime(registration.Lifetime);
            foreach (var required in registration.Registration.RequiredTypes.Distinct())
            {
                foreach (var candidate in ResolveImplementationCandidates(context, required))
                {
                    var toLifetime = NormalizeLifetime(candidate.Lifetime);
                    if (fromLifetime == "singleton" && toLifetime == "scoped")
                    {
                        warnings.Add(
                            new QudiLifetimeWarning(
                                registration.ServiceDisplay,
                                registration.ImplementationDisplay,
                                candidate.ImplementationDisplay,
                                "Singleton service may capture a Scoped dependency."
                            )
                        );
                    }
                }
            }
        }

        return warnings
            .DistinctBy(w => $"{w.Service}|{w.From}|{w.To}|{w.Message}")
            .OrderBy(w => w.Service, StringComparer.Ordinal)
            .ThenBy(w => w.From, StringComparer.Ordinal)
            .ToList();
    }

    private static List<QudiTraceResult> BuildTraces(
        VisualizationContext context,
        IReadOnlyCollection<Type> traceServices
    )
    {
        var results = new List<QudiTraceResult>();

        foreach (var traceService in traceServices)
        {
            var roots = BuildTraceForService(context, traceService, new HashSet<Type>(), 0, 8);
            results.Add(new QudiTraceResult(ToDisplayName(traceService), roots));
        }

        return results;
    }

    private static List<QudiTraceNode> BuildTraceForService(
        VisualizationContext context,
        Type serviceType,
        HashSet<Type> stack,
        int depth,
        int maxDepth
    )
    {
        if (depth > maxDepth)
        {
            return [new QudiTraceNode("...", "Max depth reached", false, false, [])];
        }

        if (!context.ServiceMap.TryGetValue(serviceType, out var candidates) || candidates.Count == 0)
        {
            return [new QudiTraceNode(ToDisplayName(serviceType), "No registration", true, false, [])];
        }

        var results = new List<QudiTraceNode>();
        foreach (var candidate in candidates.Where(c => !c.IsDecorator))
        {
            var implType = candidate.Registration.Type;
            if (!stack.Add(implType))
            {
                results.Add(
                    new QudiTraceNode(
                        candidate.ImplementationDisplay,
                        "Cycle detected",
                        false,
                        true,
                        []
                    )
                );
                continue;
            }

            var children = new List<QudiTraceNode>();
            foreach (var required in candidate.Registration.RequiredTypes.Distinct())
            {
                var dependencyChildren = BuildTraceForService(
                    context,
                    required,
                    stack,
                    depth + 1,
                    maxDepth
                );
                children.Add(new QudiTraceNode(ToDisplayName(required), null, false, false, dependencyChildren));
            }

            stack.Remove(implType);

            results.Add(
                new QudiTraceNode(
                    candidate.ImplementationDisplay,
                    $"Lifetime={candidate.Lifetime}, Key={candidate.Key}",
                    false,
                    false,
                    children
                )
            );
        }

        return results;
    }

    private static IEnumerable<RegistrationView> ResolveImplementationCandidates(
        VisualizationContext context,
        Type required
    )
    {
        if (context.ServiceMap.TryGetValue(required, out var serviceMatches))
        {
            return serviceMatches.Where(s => !s.IsDecorator);
        }

        if (context.ImplementationMap.TryGetValue(required, out var implementationMatches))
        {
            return implementationMatches.Where(s => !s.IsDecorator);
        }

        return [];
    }

    internal static bool IsApplicable(TypeRegistrationInfo registration, IReadOnlyCollection<string> conditions)
    {
        if (registration.When.Count == 0)
        {
            return true;
        }

        return registration.When.Any(r => conditions.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeLifetime(string lifetime)
    {
        return lifetime.Trim().ToLowerInvariant();
    }

    internal static string ToDisplayName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var genericName = type.Name;
        var tick = genericName.IndexOf('`');
        if (tick > 0)
        {
            genericName = genericName.Substring(0, tick);
        }

        var args = type.GetGenericArguments().Select(ToDisplayName);
        var prefix = type.Namespace is null ? string.Empty : type.Namespace + ".";
        return prefix + genericName + "<" + string.Join(", ", args) + ">";
    }

    /// <summary>
    /// Determines if a type is from an external library (e.g., System.*, Microsoft.Extensions.*).
    /// External types are typically framework or third-party types that should not be flagged as missing.
    /// </summary>
    internal static bool IsExternalType(Type type)
    {
        if (type.Namespace == null)
        {
            return false;
        }

        // Common framework and library namespaces
        var externalNamespaces = new[]
        {
            "System",
            "Microsoft.Extensions",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Newtonsoft.Json",
            "Serilog",
            "NLog",
            "log4net"
        };

        foreach (var ns in externalNamespaces)
        {
            if (type.Namespace.Equals(ns, StringComparison.Ordinal) ||
                type.Namespace.StartsWith(ns + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}