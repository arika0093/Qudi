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
            var isDecorator = registration.MarkAsDecorator;

            if (isDecorator)
            {
                // For decorators, mark them as "decorator" type
                AddNode(nodes, implLabel, "decorator");
            }
            else
            {
                AddNode(nodes, implLabel, "implementation");
            }

            var serviceTypes = registration.AsTypes.Count > 0
                ? registration.AsTypes.Distinct()
                : [registration.Type];

            foreach (var serviceType in serviceTypes)
            {
                var serviceLabel = QudiVisualizationAnalyzer.ToDisplayName(serviceType);
                AddNode(nodes, serviceLabel, "service");
                
                if (isDecorator)
                {
                    // For decorators: Service -> Decorator
                    edges.Add(new QudiVisualizationEdge(serviceLabel, implLabel, "decorator-provides"));
                }
                else
                {
                    edges.Add(new QudiVisualizationEdge(serviceLabel, implLabel, "registration"));
                }
            }

            foreach (var required in registration.RequiredTypes.Distinct())
            {
                // Check if this is a collection type (IEnumerable<T>, IList<T>, etc.)
                var elementType = TryGetCollectionElementType(required);
                if (elementType != null)
                {
                    // For collection types, create a 1:many relationship to the element type
                    var elementLabel = QudiVisualizationAnalyzer.ToDisplayName(elementType);
                    var isMissing = !registeredTypes.Contains(elementType) && !QudiVisualizationAnalyzer.IsExternalType(elementType);
                    AddNode(nodes, elementLabel, isMissing ? "missing" : "required");
                    edges.Add(new QudiVisualizationEdge(implLabel, elementLabel, "collection"));
                }
                else
                {
                    var requiredLabel = QudiVisualizationAnalyzer.ToDisplayName(required);
                    var isMissing = !registeredTypes.Contains(required) && !QudiVisualizationAnalyzer.IsExternalType(required);
                    
                    // Check if this is a decorator requiring the decorated service
                    if (isDecorator && serviceTypes.Any(s => s == required))
                    {
                        // Decorator -> Decorated Service
                        AddNode(nodes, requiredLabel, "service");
                        edges.Add(new QudiVisualizationEdge(implLabel, requiredLabel, "decorator-wraps"));
                    }
                    else
                    {
                        AddNode(nodes, requiredLabel, isMissing ? "missing" : "required");
                        edges.Add(new QudiVisualizationEdge(implLabel, requiredLabel, "dependency"));
                    }
                }
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

    /// <summary>
    /// Check if the type is a collection type and extract the element type.
    /// Returns null if the type is not a collection type.
    /// </summary>
    private static Type? TryGetCollectionElementType(Type type)
    {
        if (!type.IsGenericType)
        {
            return null;
        }

        var genericTypeDef = type.GetGenericTypeDefinition();
        
        // Check for common collection interfaces
        if (genericTypeDef == typeof(IEnumerable<>) ||
            genericTypeDef == typeof(IList<>) ||
            genericTypeDef == typeof(ICollection<>) ||
            genericTypeDef == typeof(IReadOnlyList<>) ||
            genericTypeDef == typeof(IReadOnlyCollection<>))
        {
            return type.GetGenericArguments()[0];
        }

        return null;
    }
}
