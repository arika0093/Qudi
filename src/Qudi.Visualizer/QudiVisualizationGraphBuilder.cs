using System;
using System.Collections.Generic;
using System.Linq;

namespace Qudi.Visualizer;

internal static class QudiVisualizationGraphBuilder
{
    public static QudiVisualizationGraph Build(QudiConfiguration configuration)
    {
        var allRegistrations = configuration.Registrations.ToList();
        var nodes = new Dictionary<string, QudiVisualizationNode>(StringComparer.Ordinal);
        var edges = new List<QudiVisualizationEdge>();
        var registeredTypes = new HashSet<Type>();

        // Build a map of registrations for easy access
        var registrationViews = allRegistrations.Select(registration =>
        {
            var serviceTypes = registration.AsTypes.Count > 0
                ? registration.AsTypes.Distinct().ToList()
                : [registration.Type];

            var isMatched = registration.When.Count == 0 ||
                            registration.When.Any(r => configuration.Conditions.Contains(r, StringComparer.OrdinalIgnoreCase));

            return new
            {
                Registration = registration,
                ServiceTypes = serviceTypes,
                IsMatched = isMatched
            };
        }).ToList();

        // Collect all registered types
        foreach (var view in registrationViews.Where(v => v.IsMatched))
        {
            registeredTypes.Add(view.Registration.Type);
            foreach (var service in view.ServiceTypes)
            {
                registeredTypes.Add(service);
            }
        }

        // Group decorators by service type
        var decoratorsByService = registrationViews
            .Where(v => v.Registration.MarkAsDecorator && v.IsMatched)
            .SelectMany(v => v.ServiceTypes.Select(s => (Service: s, View: v)))
            .GroupBy(x => x.Service)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.View.Registration.Order).Select(x => x.View).ToList());

        foreach (var view in registrationViews)
        {
            var registration = view.Registration;
            var implType = registration.Type;
            var implLabel = QudiVisualizationAnalyzer.ToDisplayName(implType);
            var isDecorator = registration.MarkAsDecorator;
            var isMatched = view.IsMatched;

            // Skip self-registration (AddSingleton<Service> pattern)
            var isSelfRegistration = view.ServiceTypes.Count == 1 && view.ServiceTypes[0] == implType;
            if (isSelfRegistration && !isDecorator)
            {
                // For self-registration, only create a node for the type (as both service and implementation)
                AddNode(nodes, implLabel, "service", isMatched);
            }
            else
            {
                // Add implementation node
                if (isDecorator)
                {
                    AddNode(nodes, implLabel, "decorator", isMatched);
                }
                else
                {
                    AddNode(nodes, implLabel, "implementation", isMatched);
                }

                // Add service nodes and edges
                foreach (var serviceType in view.ServiceTypes)
                {
                    var serviceLabel = QudiVisualizationAnalyzer.ToDisplayName(serviceType);
                    AddNode(nodes, serviceLabel, "service", isMatched);

                    if (isDecorator)
                    {
                        // For decorators: Service -> Decorator
                        edges.Add(new QudiVisualizationEdge(serviceLabel, implLabel, "decorator-provides"));
                    }
                    else
                    {
                        // Normal registration: Service -> Implementation
                        edges.Add(new QudiVisualizationEdge(serviceLabel, implLabel, "registration"));
                    }
                }
            }

            // Process dependencies
            foreach (var required in registration.RequiredTypes.Distinct())
            {
                // Check if this is a collection type (IEnumerable<T>, IList<T>, etc.)
                var elementType = TryGetCollectionElementType(required);
                if (elementType != null)
                {
                    // For collection types, create a 1:many relationship to the element type
                    var elementLabel = QudiVisualizationAnalyzer.ToDisplayName(elementType);
                    var isMissing = !registeredTypes.Contains(elementType) && !QudiVisualizationAnalyzer.IsExternalType(elementType);
                    AddNode(nodes, elementLabel, isMissing ? "missing" : "service", isMatched);
                    edges.Add(new QudiVisualizationEdge(implLabel, elementLabel, "collection"));
                }
                else
                {
                    var requiredLabel = QudiVisualizationAnalyzer.ToDisplayName(required);

                    // For decorators, connect to the next decorator or final implementation
                    if (isDecorator && view.ServiceTypes.Any(s => s == required))
                    {
                        // Find the next decorator or implementation in the chain
                        if (decoratorsByService.TryGetValue(required, out var decorators))
                        {
                            // Find current decorator's position
                            var currentIndex = decorators.FindIndex(d => d.Registration.Type == implType);
                            if (currentIndex >= 0 && currentIndex < decorators.Count - 1)
                            {
                                // Connect to next decorator
                                var nextDecorator = decorators[currentIndex + 1];
                                var nextLabel = QudiVisualizationAnalyzer.ToDisplayName(nextDecorator.Registration.Type);
                                AddNode(nodes, nextLabel, "decorator", isMatched);
                                edges.Add(new QudiVisualizationEdge(implLabel, nextLabel, "decorator-wraps"));
                            }
                            else
                            {
                                // Connect to final implementation
                                var implementations = registrationViews
                                    .Where(v => !v.Registration.MarkAsDecorator && 
                                               v.IsMatched &&
                                               v.ServiceTypes.Contains(required))
                                    .ToList();

                                foreach (var impl in implementations)
                                {
                                    var implImplLabel = QudiVisualizationAnalyzer.ToDisplayName(impl.Registration.Type);
                                    AddNode(nodes, implImplLabel, "implementation", isMatched);
                                    edges.Add(new QudiVisualizationEdge(implLabel, implImplLabel, "decorator-wraps"));
                                }
                            }
                        }
                    }
                    else
                    {
                        // Normal dependency
                        var isMissing = !registeredTypes.Contains(required) && !QudiVisualizationAnalyzer.IsExternalType(required);
                        AddNode(nodes, requiredLabel, isMissing ? "missing" : "service", isMatched);
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
        string kind,
        bool isConditionMatched = true
    )
    {
        if (nodes.TryGetValue(label, out var existing))
        {
            // Upgrade node kind if needed
            if (existing.Kind == "missing" && kind != "missing")
            {
                nodes[label] = new QudiVisualizationNode(label, label, kind, isConditionMatched && existing.IsConditionMatched);
            }
            else if (!existing.IsConditionMatched && isConditionMatched)
            {
                nodes[label] = new QudiVisualizationNode(label, label, existing.Kind, true);
            }
            return;
        }

        nodes[label] = new QudiVisualizationNode(label, label, kind, isConditionMatched);
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
