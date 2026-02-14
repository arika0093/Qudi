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
        var registrationViews = allRegistrations
            .Select(registration =>
            {
                var serviceTypes =
                    registration.AsTypes.Count > 0
                        ? registration.AsTypes.Distinct().ToList()
                        : [registration.Type];

                var isMatched =
                    registration.When.Count == 0
                    || registration.When.Any(r =>
                        configuration.Conditions.Contains(r, StringComparer.OrdinalIgnoreCase)
                    );

                return new
                {
                    Registration = registration,
                    ServiceTypes = serviceTypes,
                    IsMatched = isMatched,
                    Condition = registration.When.Count > 0
                        ? string.Join(", ", registration.When)
                        : null,
                };
            })
            .ToList();

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
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.View.Registration.Order).Select(x => x.View).ToList()
            );

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
            var isSelfRegistration =
                view.ServiceTypes.Count == 1 && view.ServiceTypes[0] == implType;
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

                // Add service nodes and edges (if it's a decorator, we will connect it to the decorated service later in the dependencies section)
                foreach (var serviceType in view.ServiceTypes)
                {
                    var serviceId = QudiVisualizationAnalyzer.ToFullDisplayName(serviceType);
                    AddNode(nodes, serviceType, "service", isMatched, false);

                    if (isDecorator)
                    {
                        // For decorators: Service -> Decorator only for the first decorator (lowest Order)
                        if (decoratorsByService.TryGetValue(serviceType, out var decorators))
                        {
                            var firstDecorator = decorators.FirstOrDefault();
                            if (firstDecorator?.Registration.Type == implType)
                            {
                                // This is the first decorator, add edge from service
                                var keyValue = registration.Key?.ToString();
                                edges.Add(
                                    new QudiVisualizationEdge(
                                        serviceId,
                                        implId,
                                        "registration",
                                        view.Condition,
                                        keyValue,
                                        registration.Order
                                    )
                                );
                            }
                        }
                    }
                    else
                    {
                        // Normal registration: Service -> Implementation (with condition)
                        // If a decorator exists for this service, we will connect the decorator to the service instead of the implementation,
                        // so we skip adding this edge here. The connection from the decorator to the implementation will be handled in the dependencies section.
                        var hasDecorator = decoratorsByService.ContainsKey(serviceType);
                        if (!hasDecorator)
                        {
                            var keyValue = registration.Key?.ToString();
                            edges.Add(
                                new QudiVisualizationEdge(
                                    serviceId,
                                    implId,
                                    "registration",
                                    view.Condition,
                                    keyValue,
                                    registration.Order
                                )
                            );
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
                    var isExternal = QudiVisualizationAnalyzer.IsExternalType(
                        elementType,
                        internalAssemblies
                    );
                    var isMissing = !isExternal && !registeredTypes.Contains(elementType);
                    AddNode(
                        nodes,
                        elementType,
                        isMissing ? "missing" : "service",
                        isMatched,
                        isExternal
                    );
                    edges.Add(
                        new QudiVisualizationEdge(implId, elementId, "collection", null, null, 0)
                    );
                }
                else
                {
                    var requiredId = QudiVisualizationAnalyzer.ToFullDisplayName(required);
                    var isExternal = QudiVisualizationAnalyzer.IsExternalType(
                        required,
                        internalAssemblies
                    );

                    // For decorators, connect to the decorated service through decorator chain
                    if (isDecorator && view.ServiceTypes.Any(s => s == required))
                    {
                        // Find decorators for this service
                        if (decoratorsByService.TryGetValue(required, out var decorators))
                        {
                            // Find current decorator's position
                            var currentIndex = decorators.FindIndex(d =>
                                d.Registration.Type == implType
                            );
                            if (currentIndex >= 0 && currentIndex < decorators.Count - 1)
                            {
                                // Connect to next decorator (normal edge)
                                var nextDecorator = decorators[currentIndex + 1];
                                var nextId = QudiVisualizationAnalyzer.ToFullDisplayName(
                                    nextDecorator.Registration.Type
                                );
                                AddNode(
                                    nodes,
                                    nextDecorator.Registration.Type,
                                    "decorator",
                                    isMatched,
                                    false
                                );
                                edges.Add(
                                    new QudiVisualizationEdge(
                                        implId,
                                        nextId,
                                        "registration",
                                        null,
                                        null,
                                        0
                                    )
                                );
                            }
                            else if (
                                implementationsByService.TryGetValue(
                                    required,
                                    out var implementations
                                )
                            )
                            {
                                // Connect to final implementation(s)
                                foreach (var impl in implementations)
                                {
                                    var implImplId = QudiVisualizationAnalyzer.ToFullDisplayName(
                                        impl.Registration.Type
                                    );
                                    AddNode(
                                        nodes,
                                        impl.Registration.Type,
                                        "implementation",
                                        isMatched,
                                        false
                                    );
                                    edges.Add(
                                        new QudiVisualizationEdge(
                                            implId,
                                            implImplId,
                                            "registration",
                                            null,
                                            null,
                                            0
                                        )
                                    );
                                }
                            }
                        }
                        else if (
                            implementationsByService.TryGetValue(required, out var implementations)
                        )
                        {
                            // No other decorators, connect directly to implementation(s)
                            foreach (var impl in implementations)
                            {
                                var implImplId = QudiVisualizationAnalyzer.ToFullDisplayName(
                                    impl.Registration.Type
                                );
                                AddNode(
                                    nodes,
                                    impl.Registration.Type,
                                    "implementation",
                                    isMatched,
                                    false
                                );
                                edges.Add(
                                    new QudiVisualizationEdge(
                                        implId,
                                        implImplId,
                                        "registration",
                                        null,
                                        null,
                                        0
                                    )
                                );
                            }
                        }
                    }
                    else
                    {
                        // Normal dependency
                        var isMissing = !isExternal && !registeredTypes.Contains(required);
                        AddNode(
                            nodes,
                            required,
                            isMissing ? "missing" : "service",
                            isMatched,
                            isExternal
                        );
                        edges.Add(
                            new QudiVisualizationEdge(
                                implId,
                                requiredId,
                                "dependency",
                                null,
                                null,
                                0
                            )
                        );
                    }
                }
            }
        }

        // Handle open generic types (e.g., NullComponentValidator<T>) for generic services
        var openGenericImplementations = registrationViews
            .Where(v =>
                !v.Registration.MarkAsDecorator
                && v.IsMatched
                && v.Registration.Type.IsGenericTypeDefinition
            )
            .ToList();

        System.Diagnostics.Debug.WriteLine(
            $"Found {openGenericImplementations.Count} open generic implementations"
        );
        foreach (var og in openGenericImplementations)
        {
            System.Diagnostics.Debug.WriteLine($"  - {og.Registration.Type.Name}");
        }

        // Find all generic service nodes and connect them to open generic implementations
        var candidateServiceTypes = new HashSet<Type>();
        foreach (var view in registrationViews)
        {
            foreach (var serviceType in view.ServiceTypes)
            {
                candidateServiceTypes.Add(serviceType);
            }
        }

        foreach (var registration in allRegistrations)
        {
            foreach (var requiredType in registration.RequiredTypes)
            {
                candidateServiceTypes.Add(requiredType);
                var elementType = TryGetCollectionElementType(requiredType);
                if (elementType != null)
                {
                    candidateServiceTypes.Add(elementType);
                }
            }
        }

        foreach (var node in nodes.Values.ToList())
        {
            var nodeType = FindTypeFromNodeId(node.Id, allRegistrations);
            if (
                nodeType == null
                || !candidateServiceTypes.Contains(nodeType)
                || !nodeType.IsGenericType
                || nodeType.IsGenericTypeDefinition
            )
            {
                continue;
            }

            var genericTypeDef = nodeType.GetGenericTypeDefinition();
            var typeArgs = nodeType.GetGenericArguments();

            foreach (var openGenericView in openGenericImplementations)
            {
                var openGenericType = openGenericView.Registration.Type;

                try
                {
                    var implementedInterfaces = openGenericType.GetInterfaces();
                    var matchingInterface = implementedInterfaces.FirstOrDefault(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDef
                    );

                    if (matchingInterface == null)
                    {
                        continue;
                    }

                    var openImplId = QudiVisualizationAnalyzer.ToFullDisplayName(openGenericType);

                    AddNode(nodes, openGenericType, "implementation", true, false);

                    var keyValue = openGenericView.Registration.Key?.ToString();
                    edges.Add(
                        new QudiVisualizationEdge(
                            node.Id,
                            openImplId,
                            "registration",
                            openGenericView.Condition,
                            keyValue,
                            openGenericView.Registration.Order
                        )
                    );
                }
                catch (ArgumentException)
                {
                    // MakeGenericType can throw if type arguments don't satisfy constraints
                    // Skip this implementation
                }
            }
        }

        var distinctEdges = edges
            .DistinctBy(e => $"{e.From}|{e.To}|{e.Kind}|{e.Condition ?? ""}")
            .ToList();

        return new QudiVisualizationGraph(nodes.Values.ToList(), distinctEdges);
    }

    private static Type? FindTypeFromNodeId(
        string nodeId,
        IReadOnlyList<TypeRegistrationInfo> registrations
    )
    {
        // Try to find the type from registrations
        foreach (var registration in registrations)
        {
            if (QudiVisualizationAnalyzer.ToFullDisplayName(registration.Type) == nodeId)
            {
                return registration.Type;
            }

            foreach (var asType in registration.AsTypes)
            {
                if (QudiVisualizationAnalyzer.ToFullDisplayName(asType) == nodeId)
                {
                    return asType;
                }
            }

            foreach (var requiredType in registration.RequiredTypes)
            {
                if (QudiVisualizationAnalyzer.ToFullDisplayName(requiredType) == nodeId)
                {
                    return requiredType;
                }

                var requiredElementType = TryGetCollectionElementType(requiredType);
                if (
                    requiredElementType != null
                    && QudiVisualizationAnalyzer.ToFullDisplayName(requiredElementType) == nodeId
                )
                {
                    return requiredElementType;
                }
            }
        }

        return null;
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
        var isInterface = type.IsInterface;

        if (nodes.TryGetValue(id, out var existing))
        {
            // Upgrade node kind if needed
            if (existing.Kind == "missing" && kind != "missing")
            {
                nodes[id] = new QudiVisualizationNode(
                    id,
                    label,
                    kind,
                    isConditionMatched || existing.IsConditionMatched,
                    isExternal || existing.IsExternal,
                    isInterface || existing.IsInterface
                );
            }
            else if (!existing.IsConditionMatched && isConditionMatched)
            {
                nodes[id] = new QudiVisualizationNode(
                    id,
                    label,
                    existing.Kind,
                    true,
                    isExternal || existing.IsExternal,
                    isInterface || existing.IsInterface
                );
            }
            else if (!existing.IsExternal && isExternal)
            {
                nodes[id] = new QudiVisualizationNode(
                    id,
                    label,
                    existing.Kind,
                    existing.IsConditionMatched,
                    true,
                    isInterface || existing.IsInterface
                );
            }
            return;
        }

        nodes[id] = new QudiVisualizationNode(
            id,
            label,
            kind,
            isConditionMatched,
            isExternal,
            isInterface
        );
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
        if (
            genericTypeDef == typeof(IEnumerable<>)
            || genericTypeDef == typeof(IList<>)
            || genericTypeDef == typeof(ICollection<>)
            || genericTypeDef == typeof(IReadOnlyList<>)
            || genericTypeDef == typeof(IReadOnlyCollection<>)
        )
        {
            return type.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>
    /// Build a subgraph starting from a specific root type.
    /// Only includes nodes reachable from the root.
    /// </summary>
    public static QudiVisualizationGraph BuildFromRoot(
        QudiConfiguration configuration,
        Type rootType
    )
    {
        var fullGraph = Build(configuration);

        // Find all reachable nodes from the root
        var rootId = QudiVisualizationAnalyzer.ToFullDisplayName(rootType);
        var reachableNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        // Start from root
        if (fullGraph.Nodes.Any(n => n.Id == rootId))
        {
            queue.Enqueue(rootId);
            reachableNodeIds.Add(rootId);
        }
        else
        {
            // Root not found, return empty graph
            return new QudiVisualizationGraph([], []);
        }

        // BFS to find all reachable nodes
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            // Find all outgoing edges from current node
            foreach (var edge in fullGraph.Edges.Where(e => e.From == currentId))
            {
                if (!reachableNodeIds.Contains(edge.To))
                {
                    reachableNodeIds.Add(edge.To);
                    queue.Enqueue(edge.To);
                }
            }
        }

        // Filter nodes and edges
        var filteredNodes = fullGraph.Nodes.Where(n => reachableNodeIds.Contains(n.Id)).ToList();

        var filteredEdges = fullGraph
            .Edges.Where(e => reachableNodeIds.Contains(e.From) && reachableNodeIds.Contains(e.To))
            .ToList();

        return new QudiVisualizationGraph(filteredNodes, filteredEdges);
    }
}
