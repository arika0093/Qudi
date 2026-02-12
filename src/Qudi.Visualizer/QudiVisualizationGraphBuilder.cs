using System;
using System.Collections.Generic;
using System.Linq;

namespace Qudi.Visualizer;

internal static class QudiVisualizationGraphBuilder
{
    public static QudiVisualizationGraph Build(QudiConfiguration configuration)
    {
        var applicable = configuration
            .Registrations.Where(r => QudiVisualizationAnalyzer.IsApplicable(r, configuration.Conditions))
            .ToList();

        var nodes = new Dictionary<string, QudiVisualizationNode>(StringComparer.Ordinal);
        var edges = new List<QudiVisualizationEdge>();
        var registeredTypes = new HashSet<Type>();

        foreach (var registration in applicable)
        {
            registeredTypes.Add(registration.Type);
            var serviceTypes = registration.AsTypes.Count > 0
                ? registration.AsTypes.Distinct()
                : [registration.Type];
            foreach (var service in serviceTypes)
            {
                registeredTypes.Add(service);
            }
        }

        foreach (var registration in applicable)
        {
            var implLabel = QudiVisualizationAnalyzer.ToDisplayName(registration.Type);
            AddNode(nodes, implLabel, "implementation");

            var serviceTypes = registration.AsTypes.Count > 0
                ? registration.AsTypes.Distinct()
                : [registration.Type];

            foreach (var serviceType in serviceTypes)
            {
                var serviceLabel = QudiVisualizationAnalyzer.ToDisplayName(serviceType);
                AddNode(nodes, serviceLabel, "service");
                edges.Add(new QudiVisualizationEdge(serviceLabel, implLabel, "registration"));
            }

            foreach (var required in registration.RequiredTypes.Distinct())
            {
                var requiredLabel = QudiVisualizationAnalyzer.ToDisplayName(required);
                var isMissing = !registeredTypes.Contains(required);
                AddNode(nodes, requiredLabel, isMissing ? "missing" : "required");
                edges.Add(new QudiVisualizationEdge(implLabel, requiredLabel, "dependency"));
            }
        }

        var distinctEdges = edges
            .DistinctBy(e => $"{e.From}|{e.To}|{e.Kind}")
            .ToList();

        return new QudiVisualizationGraph(nodes.Values.ToList(), distinctEdges);
    }

    private static void AddNode(
        IDictionary<string, QudiVisualizationNode> nodes,
        string label,
        string kind
    )
    {
        if (nodes.TryGetValue(label, out var existing))
        {
            if (existing.Kind == "missing" && kind != "missing")
            {
                nodes[label] = existing with { Kind = kind };
            }
            return;
        }

        nodes[label] = new QudiVisualizationNode(label, label, kind);
    }
}
