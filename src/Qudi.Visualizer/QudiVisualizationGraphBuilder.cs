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

        var compositeTypes = registrationViews
            .Where(v => v.Registration.MarkAsComposite && v.IsMatched)
            .Select(v => v.Registration.Type)
            .ToHashSet();

        // Group decorators and composites by service type in application order (outer -> inner).
        var layersByService = registrationViews
            .Where(v =>
                (v.Registration.MarkAsDecorator || v.Registration.MarkAsComposite) && v.IsMatched
            )
            .SelectMany(v => v.ServiceTypes.Select(s => (Service: s, View: v)))
            .GroupBy(x => x.Service)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.View.Registration.Order)
                    // Lower order is outer; for the same order, decorators wrap composites.
                    .ThenBy(x => x.View.Registration.MarkAsComposite ? 1 : 0)
                    .ThenBy(x => x.View.Registration.Type.FullName, StringComparer.Ordinal)
                    .Select(x => x.View)
                    .ToList()
            );


        // Build a map of implementations by service type
        var implementationsByService = registrationViews
            .Where(v => !v.Registration.MarkAsDecorator && v.IsMatched)
            .SelectMany(v => v.ServiceTypes.Select(s => (Service: s, View: v)))
            .GroupBy(x => x.Service)
            .ToDictionary(g => g.Key, g => g.Select(x => x.View).ToList());

        // Non-decorator, non-composite implementations (used for composite inner services)
        var baseImplementationsByService = registrationViews
            .Where(v =>
                !v.Registration.MarkAsDecorator && !v.Registration.MarkAsComposite && v.IsMatched
            )
            .SelectMany(v => v.ServiceTypes.Select(s => (Service: s, View: v)))
            .GroupBy(x => x.Service)
            .ToDictionary(g => g.Key, g => g.Select(x => x.View).ToList());

        foreach (var view in registrationViews)
        {
            var registration = view.Registration;
            var implType = registration.Type;
            var implId = QudiVisualizationAnalyzer.ToFullDisplayName(implType);
            var isDecorator = registration.MarkAsDecorator;
            var isComposite = registration.MarkAsComposite;
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
                else if (isComposite)
                {
                    AddNode(nodes, implType, "composite", isMatched, false);
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

                    if (isDecorator || isComposite)
                    {
                        // For layers: Service -> first layer only (lowest Order, decorator wraps composite when equal).
                        if (layersByService.TryGetValue(serviceType, out var layers))
                        {
                            var firstLayer = layers.FirstOrDefault();
                            if (firstLayer?.Registration.Type == implType)
                            {
                                // This is the first layer, add edge from service
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
                        var hasLayers = layersByService.ContainsKey(serviceType);
                        if (hasLayers)
                        {
                            // If any layer exists, only show Service -> first layer (hide direct links to base implementations)
                            continue;
                        }

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

            // Process dependencies
            foreach (var required in registration.RequiredTypes.Distinct())
            {
                // Check if this is a collection type (IEnumerable<T>, IList<T>, etc.)
                var elementType = TryGetCollectionElementType(required);
                if (elementType != null)
                {
                    // For composite collections that represent the composite's own service type,
                    // connect to the underlying implementations instead of the service interface.
                    var isCompositeCollection =
                        registration.MarkAsComposite && view.ServiceTypes.Contains(elementType);

                    if (isCompositeCollection)
                    {
                        // Prefer the next inner layer (by Order) if it exists; otherwise connect to base implementations.
                        var handled = false;
                        if (layersByService.TryGetValue(elementType, out var layers))
                        {
                            var nextLayer = layers
                                .Where(l =>
                                    l.Registration.Type != registration.Type
                                    && l.Registration.Order > registration.Order
                                )
                                .OrderBy(l => l.Registration.Order)
                                .ThenBy(l => l.Registration.MarkAsComposite ? 1 : 0)
                                .ThenBy(
                                    l => l.Registration.Type.FullName,
                                    StringComparer.Ordinal
                                )
                                .FirstOrDefault();
                            if (nextLayer is not null)
                            {
                                var nextType = nextLayer.Registration.Type;
                                var nextId = QudiVisualizationAnalyzer.ToFullDisplayName(nextType);
                                AddNode(
                                    nodes,
                                    nextType,
                                    nextLayer.Registration.MarkAsComposite
                                        ? "composite"
                                        : "decorator",
                                    isMatched,
                                    false
                                );
                                edges.Add(
                                    new QudiVisualizationEdge(
                                        implId,
                                        nextId,
                                        "collection",
                                        null,
                                        null,
                                        0
                                    )
                                );
                                handled = true;
                            }
                        }

                        if (
                            !handled
                            && baseImplementationsByService.TryGetValue(
                                elementType,
                                out var baseImplementations
                            )
                        )
                        {
                            foreach (
                                var implementationType in baseImplementations.Select(impl =>
                                    impl.Registration.Type
                                )
                            )
                            {
                                var implImplId = QudiVisualizationAnalyzer.ToFullDisplayName(
                                    implementationType
                                );
                                AddNode(
                                    nodes,
                                    implementationType,
                                    "implementation",
                                    isMatched,
                                    false
                                );
                                edges.Add(
                                    new QudiVisualizationEdge(
                                        implId,
                                        implImplId,
                                        "collection",
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
                            new QudiVisualizationEdge(
                                implId,
                                elementId,
                                "collection",
                                null,
                                null,
                                0
                            )
                        );
                    }
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
                        if (layersByService.TryGetValue(required, out var layers))
                        {
                            var nextLayer = layers
                                .Where(l =>
                                    l.Registration.Type != implType
                                    && (
                                        l.Registration.Order > registration.Order
                                        || (
                                            l.Registration.Order == registration.Order
                                            && registration.MarkAsDecorator
                                            && l.Registration.MarkAsComposite
                                        )
                                    )
                                )
                                .OrderBy(l => l.Registration.Order)
                                .ThenBy(l => l.Registration.MarkAsComposite ? 1 : 0)
                                .ThenBy(
                                    l => l.Registration.Type.FullName,
                                    StringComparer.Ordinal
                                )
                                .FirstOrDefault();
                            if (
                                nextLayer is not null
                                && !(
                                    nextLayer.Registration.MarkAsComposite
                                    && nextLayer.Registration.Order < registration.Order
                                )
                            )
                            {
                                // Connect to next layer (decorator or composite)
                                var nextId = QudiVisualizationAnalyzer.ToFullDisplayName(
                                    nextLayer.Registration.Type
                                );
                                AddNode(
                                    nodes,
                                    nextLayer.Registration.Type,
                                    nextLayer.Registration.MarkAsComposite
                                        ? "composite"
                                        : "decorator",
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
                                baseImplementationsByService.TryGetValue(
                                    required,
                                    out var implementations
                                )
                            )
                            {
                                // Connect to final base implementation(s) to avoid cycles with composites
                                foreach (
                                    var implementationType in implementations.Select(impl =>
                                        impl.Registration.Type
                                    )
                                )
                                {
                                    var implImplId =
                                        QudiVisualizationAnalyzer.ToFullDisplayName(
                                            implementationType
                                        );
                                    AddNode(
                                        nodes,
                                        implementationType,
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
                            baseImplementationsByService.TryGetValue(
                                required,
                                out var implementations
                            )
                        )
                        {
                            // No other layers, connect directly to base implementation(s)
                            foreach (
                                var implementationType in implementations.Select(impl =>
                                    impl.Registration.Type
                                )
                            )
                            {
                                var implImplId = QudiVisualizationAnalyzer.ToFullDisplayName(
                                    implementationType
                                );
                                AddNode(
                                    nodes,
                                    implementationType,
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
                        if (isDecorator && compositeTypes.Contains(required))
                        {
                            continue;
                        }
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

        foreach (var nodeId in nodes.Values.Select(node => node.Id).ToList())
        {
            var nodeType = FindTypeFromNodeId(nodeId, allRegistrations);
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
            foreach (var openGenericView in openGenericImplementations)
            {
                var openGenericType = openGenericView.Registration.Type;

                var declaredServiceTypes =
                    openGenericView.ServiceTypes.Count > 0
                        ? openGenericView.ServiceTypes
                        : [openGenericType];

                var hasMatchingServiceType = declaredServiceTypes.Any(serviceType =>
                    serviceType.IsGenericType
                        ? serviceType.GetGenericTypeDefinition() == genericTypeDef
                        : serviceType == genericTypeDef
                );

                if (!hasMatchingServiceType)
                {
                    continue;
                }

                var hasClosedImplementation =
                    implementationsByService.TryGetValue(nodeType, out var closedImplementations)
                    && closedImplementations.Any(impl =>
                        !impl.Registration.Type.IsGenericTypeDefinition
                    );
                if (hasClosedImplementation)
                {
                    continue;
                }

                var openImplId = QudiVisualizationAnalyzer.ToFullDisplayName(openGenericType);

                AddNode(nodes, openGenericType, "implementation", true, false);

                var keyValue = openGenericView.Registration.Key?.ToString();
                edges.Add(
                    new QudiVisualizationEdge(
                        nodeId,
                        openImplId,
                        "registration",
                        openGenericView.Condition,
                        keyValue,
                        openGenericView.Registration.Order
                    )
                );
            }
        }

        // Remove accidental cycles: Decorator -> Composite edges are never part of the intended chain.
        edges.RemoveAll(e =>
            nodes.TryGetValue(e.From, out var fromNode)
            && nodes.TryGetValue(e.To, out var toNode)
            && fromNode.Kind == "decorator"
            && toNode.Kind == "composite"
        );

        var distinctEdges = edges
            .DistinctBy(e =>
                $"{e.From}|{e.To}|{e.Kind}|{e.Condition ?? ""}|{e.Key ?? ""}|{e.Order}"
            )
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

            var matchingAsType = registration.AsTypes.FirstOrDefault(asType =>
                QudiVisualizationAnalyzer.ToFullDisplayName(asType) == nodeId
            );
            if (matchingAsType != null)
            {
                return matchingAsType;
            }

            var matchingRequiredType = registration.RequiredTypes.FirstOrDefault(requiredType =>
                QudiVisualizationAnalyzer.ToFullDisplayName(requiredType) == nodeId
            );
            if (matchingRequiredType != null)
            {
                return matchingRequiredType;
            }

            var matchingRequiredElementType = registration
                .RequiredTypes.Select(TryGetCollectionElementType)
                .FirstOrDefault(requiredElementType =>
                    requiredElementType != null
                    && QudiVisualizationAnalyzer.ToFullDisplayName(requiredElementType) == nodeId
                );
            if (matchingRequiredElementType != null)
            {
                return matchingRequiredElementType;
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
            if (
                (existing.Kind == "missing" && kind != "missing")
                || IsUpgradeKind(existing.Kind, kind)
            )
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

    private static bool IsUpgradeKind(string existingKind, string newKind)
    {
        // Ensure composites/decorators keep their visual style even if they were added earlier
        // as generic implementations.
        if ((newKind == "composite" || newKind == "decorator") && existingKind == "implementation")
        {
            return true;
        }

        return false;
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
            foreach (
                var toId in fullGraph
                    .Edges.Where(e => e.From == currentId)
                    .Select(e => e.To)
                    .Where(toId => !reachableNodeIds.Contains(toId))
            )
            {
                reachableNodeIds.Add(toId);
                queue.Enqueue(toId);
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
