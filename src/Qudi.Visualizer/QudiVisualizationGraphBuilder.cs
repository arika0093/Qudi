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

        // Build internal assemblies set
        var internalAssemblies = allRegistrations
            .Select(r => r.AssemblyName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

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
                IsMatched = isMatched,
                Condition = registration.When.Count > 0 ? string.Join(", ", registration.When) : null
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

        // Build a map of implementations by service type
        var implementationsByService = registrationViews
            .Where(v => !v.Registration.MarkAsDecorator && v.IsMatched)
            .SelectMany(v => v.ServiceTypes.Select(s => (Service: s, View: v)))
            .GroupBy(x => x.Service)
            .ToDictionary(g => g.Key, g => g.Select(x => x.View).ToList());

        foreach (var view in registrationViews)
        {
            var registration = view.Registration;
            var implType = registration.Type;
            var implId = QudiVisualizationAnalyzer.ToFullDisplayName(implType);
            var isDecorator = registration.MarkAsDecorator;
            var isMatched = view.IsMatched;

            // Skip self-registration (AddSingleton<Service> pattern)
            var isSelfRegistration = view.ServiceTypes.Count == 1 && view.ServiceTypes[0] == implType;
            if (isSelfRegistration && !isDecorator)
            {
                // For self-registration, only create a node for the type (as both service and implementation)
                AddNode(nodes, implType, "service", isMatched, false);
            }
            else
            {
                // Add implementation node
                if (isDecorator)
                {
                    AddNode(nodes, implType, "decorator", isMatched, false);
                }
                else
                {
                    AddNode(nodes, implType, "implementation", isMatched, false);
                }

                // Add service nodes and edges (デコレーターの場合は直接の登録線を引かない)
                foreach (var serviceType in view.ServiceTypes)
                {
                    var serviceId = QudiVisualizationAnalyzer.ToFullDisplayName(serviceType);
                    AddNode(nodes, serviceType, "service", isMatched, false);

                    if (isDecorator)
                    {
                        // For decorators: Service -> Decorator (with condition), 普通の線に変更
                        edges.Add(new QudiVisualizationEdge(serviceId, implId, "registration", view.Condition));
                    }
                    else
                    {
                        // Normal registration: Service -> Implementation (with condition)
                        // デコレーターが存在する場合は線を引かない
                        var hasDecorator = decoratorsByService.ContainsKey(serviceType);
                        if (!hasDecorator)
                        {
                            edges.Add(new QudiVisualizationEdge(serviceId, implId, "registration", view.Condition));
                        }
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
                    var elementId = QudiVisualizationAnalyzer.ToFullDisplayName(elementType);
                    var isExternal = QudiVisualizationAnalyzer.IsExternalType(elementType, internalAssemblies);
                    var isMissing = !isExternal && !registeredTypes.Contains(elementType);
                    AddNode(nodes, elementType, isMissing ? "missing" : "service", isMatched, isExternal);
                    edges.Add(new QudiVisualizationEdge(implId, elementId, "collection", null));
                }
                else
                {
                    var requiredId = QudiVisualizationAnalyzer.ToFullDisplayName(required);
                    var isExternal = QudiVisualizationAnalyzer.IsExternalType(required, internalAssemblies);

                    // For decorators, connect to the decorated service through decorator chain
                    if (isDecorator && view.ServiceTypes.Any(s => s == required))
                    {
                        // Find decorators for this service
                        if (decoratorsByService.TryGetValue(required, out var decorators))
                        {
                            // Find current decorator's position
                            var currentIndex = decorators.FindIndex(d => d.Registration.Type == implType);
                            if (currentIndex >= 0 && currentIndex < decorators.Count - 1)
                            {
                                // Connect to next decorator (普通の線)
                                var nextDecorator = decorators[currentIndex + 1];
                                var nextId = QudiVisualizationAnalyzer.ToFullDisplayName(nextDecorator.Registration.Type);
                                AddNode(nodes, nextDecorator.Registration.Type, "decorator", isMatched, false);
                                edges.Add(new QudiVisualizationEdge(implId, nextId, "registration", null));
                            }
                            else if (implementationsByService.TryGetValue(required, out var implementations))
                            {
                                // Connect to final implementation(s) (普通の線)
                                foreach (var impl in implementations)
                                {
                                    var implImplId = QudiVisualizationAnalyzer.ToFullDisplayName(impl.Registration.Type);
                                    AddNode(nodes, impl.Registration.Type, "implementation", isMatched, false);
                                    edges.Add(new QudiVisualizationEdge(implId, implImplId, "registration", null));
                                }
                            }
                        }
                        else if (implementationsByService.TryGetValue(required, out var implementations))
                        {
                            // No other decorators, connect directly to implementation(s) (普通の線)
                            foreach (var impl in implementations)
                            {
                                var implImplId = QudiVisualizationAnalyzer.ToFullDisplayName(impl.Registration.Type);
                                AddNode(nodes, impl.Registration.Type, "implementation", isMatched, false);
                                edges.Add(new QudiVisualizationEdge(implId, implImplId, "registration", null));
                            }
                        }
                    }
                    else
                    {
                        // Normal dependency
                        var isMissing = !isExternal && !registeredTypes.Contains(required);
                        AddNode(nodes, required, isMissing ? "missing" : "service", isMatched, isExternal);
                        edges.Add(new QudiVisualizationEdge(implId, requiredId, "dependency", null));
                    }
                }
            }
        }

        var distinctEdges = edges
            .DistinctBy(e => $"{e.From}|{e.To}|{e.Kind}|{e.Condition ?? ""}")
            .ToList();

        return new QudiVisualizationGraph(nodes.Values.ToList(), distinctEdges);
    }

    private static void AddNode(
        IDictionary<string, QudiVisualizationNode> nodes,
        Type type,
        string kind,
        bool isConditionMatched = true,
        bool isExternal = false
    )
    {
        var id = QudiVisualizationAnalyzer.ToFullDisplayName(type);
        var label = QudiVisualizationAnalyzer.ToDisplayName(type);

        // Determine actual kind based on type
        var actualKind = kind;
        if (kind == "service" || kind == "implementation")
        {
            actualKind = type.IsInterface ? "interface" : "class";
        }
        else if (kind == "decorator")
        {
            actualKind = "decorator";
        }

        if (nodes.TryGetValue(id, out var existing))
        {
            // Upgrade node kind if needed
            if (existing.Kind == "missing" && actualKind != "missing")
            {
                nodes[id] = new QudiVisualizationNode(id, label, actualKind, isConditionMatched && existing.IsConditionMatched, isExternal || existing.IsExternal);
            }
            else if (!existing.IsConditionMatched && isConditionMatched)
            {
                nodes[id] = new QudiVisualizationNode(id, label, existing.Kind, true, isExternal || existing.IsExternal);
            }
            else if (!existing.IsExternal && isExternal)
            {
                nodes[id] = new QudiVisualizationNode(id, label, existing.Kind, existing.IsConditionMatched, true);
            }
            return;
        }

        nodes[id] = new QudiVisualizationNode(id, label, actualKind, isConditionMatched, isExternal);
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
