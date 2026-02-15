using System;
using System.Collections.Generic;
using System.Linq;

namespace Qudi;

/// <summary>
/// Builds dependency graphs from Qudi registrations.
/// </summary>
public static class QudiVisualizationGraphBuilder
{
    /// <summary>
    /// Build a graph from all registrations.
    /// </summary>
    public static QudiVisualizationGraph Build(QudiConfiguration configuration)
    {
        var allRegistrations = configuration.Registrations.ToList();
        var nodes = new Dictionary<string, QudiVisualizationNode>(StringComparer.Ordinal);
        var edges = new List<QudiVisualizationEdge>();
        var registeredTypes = new HashSet<Type>();

        var internalAssemblies = allRegistrations
            .Select(r => r.AssemblyName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

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

        foreach (var view in registrationViews.Where(v => v.IsMatched))
        {
            registeredTypes.Add(view.Registration.Type);
            foreach (var service in view.ServiceTypes)
            {
                registeredTypes.Add(service);
            }
        }

        var decoratorsByService = registrationViews
            .Where(v => v.Registration.MarkAsDecorator && v.IsMatched)
            .SelectMany(v => v.ServiceTypes.Select(s => (Service: s, View: v)))
            .GroupBy(x => x.Service)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.View.Registration.Order).Select(x => x.View).ToList()
            );

        var implementationsByService = registrationViews
            .Where(v => !v.Registration.MarkAsDecorator && v.IsMatched)
            .SelectMany(v => v.ServiceTypes.Select(s => (Service: s, View: v)))
            .GroupBy(x => x.Service)
            .ToDictionary(g => g.Key, g => g.Select(x => x.View).ToList());

        foreach (var view in registrationViews)
        {
            var registration = view.Registration;
            var implType = registration.Type;
            var implId = ToFullDisplayName(implType);
            var isDecorator = registration.MarkAsDecorator;
            var isMatched = view.IsMatched;

            var isSelfRegistration =
                view.ServiceTypes.Count == 1 && view.ServiceTypes[0] == implType;
            if (isSelfRegistration && !isDecorator)
            {
                AddNode(nodes, implType, "service", isMatched, false);
            }
            else
            {
                AddNode(
                    nodes,
                    implType,
                    isDecorator ? "decorator" : "implementation",
                    isMatched,
                    false
                );

                foreach (var serviceType in view.ServiceTypes)
                {
                    var serviceId = ToFullDisplayName(serviceType);
                    AddNode(nodes, serviceType, "service", isMatched, false);

                    if (isDecorator)
                    {
                        if (decoratorsByService.TryGetValue(serviceType, out var decorators))
                        {
                            var firstDecorator = decorators.FirstOrDefault();
                            if (firstDecorator?.Registration.Type == implType)
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
                    else
                    {
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

            foreach (var required in registration.RequiredTypes.Distinct())
            {
                var elementType = TryGetCollectionElementType(required);
                if (elementType != null)
                {
                    var elementId = ToFullDisplayName(elementType);
                    var isExternal = IsExternalType(elementType, internalAssemblies);
                    var isMissing = !isExternal && !registeredTypes.Contains(elementType);
                    AddNode(
                        nodes,
                        elementType,
                        isMissing ? "missing" : "service",
                        isMatched,
                        isExternal
                    );
                    edges.Add(new QudiVisualizationEdge(implId, elementId, "collection", null, null, 0));
                }
                else
                {
                    var requiredId = ToFullDisplayName(required);
                    var isExternal = IsExternalType(required, internalAssemblies);

                    if (isDecorator && view.ServiceTypes.Any(s => s == required))
                    {
                        if (decoratorsByService.TryGetValue(required, out var decorators))
                        {
                            var currentIndex = decorators.FindIndex(d =>
                                d.Registration.Type == implType
                            );
                            if (currentIndex >= 0 && currentIndex < decorators.Count - 1)
                            {
                                var nextDecorator = decorators[currentIndex + 1];
                                var nextId = ToFullDisplayName(nextDecorator.Registration.Type);
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
                                implementationsByService.TryGetValue(required, out var implementations)
                            )
                            {
                                foreach (var impl in implementations)
                                {
                                    var implImplId = ToFullDisplayName(impl.Registration.Type);
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
                            foreach (var impl in implementations)
                            {
                                var implImplId = ToFullDisplayName(impl.Registration.Type);
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
                        var isMissing = !isExternal && !registeredTypes.Contains(required);
                        AddNode(
                            nodes,
                            required,
                            isMissing ? "missing" : "service",
                            isMatched,
                            isExternal
                        );
                        edges.Add(new QudiVisualizationEdge(implId, requiredId, "dependency", null, null, 0));
                    }
                }
            }
        }

        var openGenericImplementations = registrationViews
            .Where(v =>
                !v.Registration.MarkAsDecorator
                && v.IsMatched
                && v.Registration.Type.IsGenericTypeDefinition
            )
            .ToList();

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

                var openImplId = ToFullDisplayName(openGenericType);

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
        }

        var distinctEdges = edges
            .DistinctBy(e => $"{e.From}|{e.To}|{e.Kind}|{e.Condition ?? ""}|{e.Key ?? ""}|{e.Order}")
            .ToList();

        return new QudiVisualizationGraph(nodes.Values.ToList(), distinctEdges);
    }

    /// <summary>
    /// Build a subgraph starting from a specific root type.
    /// </summary>
    public static QudiVisualizationGraph BuildFromRoot(QudiConfiguration configuration, Type rootType)
    {
        var fullGraph = Build(configuration);
        var rootId = ToFullDisplayName(rootType);
        var reachableNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        if (fullGraph.Nodes.Any(n => n.Id == rootId))
        {
            queue.Enqueue(rootId);
            reachableNodeIds.Add(rootId);
        }
        else
        {
            return new QudiVisualizationGraph([], []);
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            foreach (var edge in fullGraph.Edges.Where(e => e.From == currentId))
            {
                if (!reachableNodeIds.Contains(edge.To))
                {
                    reachableNodeIds.Add(edge.To);
                    queue.Enqueue(edge.To);
                }
            }
        }

        var filteredNodes = fullGraph.Nodes.Where(n => reachableNodeIds.Contains(n.Id)).ToList();
        var filteredEdges = fullGraph
            .Edges.Where(e => reachableNodeIds.Contains(e.From) && reachableNodeIds.Contains(e.To))
            .ToList();

        return new QudiVisualizationGraph(filteredNodes, filteredEdges);
    }

    private static Type? FindTypeFromNodeId(
        string nodeId,
        IReadOnlyList<TypeRegistrationInfo> registrations
    )
    {
        foreach (var registration in registrations)
        {
            if (ToFullDisplayName(registration.Type) == nodeId)
            {
                return registration.Type;
            }

            foreach (var asType in registration.AsTypes)
            {
                if (ToFullDisplayName(asType) == nodeId)
                {
                    return asType;
                }
            }

            foreach (var requiredType in registration.RequiredTypes)
            {
                if (ToFullDisplayName(requiredType) == nodeId)
                {
                    return requiredType;
                }

                var requiredElementType = TryGetCollectionElementType(requiredType);
                if (requiredElementType != null && ToFullDisplayName(requiredElementType) == nodeId)
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
        var id = ToFullDisplayName(type);
        var label = ToDisplayName(type);
        var isInterface = type.IsInterface;

        if (nodes.TryGetValue(id, out var existing))
        {
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

    private static Type? TryGetCollectionElementType(Type type)
    {
        if (!type.IsGenericType)
        {
            return null;
        }

        var genericTypeDef = type.GetGenericTypeDefinition();
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

    private static string ToDisplayName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var genericName = type.Name;
        var tick = genericName.IndexOf('`');
        if (tick > 0)
        {
            genericName = genericName.Substring(0, tick);
        }

        var args = type.GetGenericArguments().Select(ToDisplayName);
        return genericName + "<" + string.Join(", ", args) + ">";
    }

    private static string ToFullDisplayName(Type type)
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

        var args = type.GetGenericArguments().Select(ToFullDisplayName);
        var prefix = type.Namespace is null ? string.Empty : type.Namespace + ".";
        return prefix + genericName + "<" + string.Join(", ", args) + ">";
    }

    private static bool IsExternalType(Type type, HashSet<string> internalAssemblies)
    {
        if (type.Namespace == null)
        {
            return false;
        }

        var assemblyName = type.Assembly.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName))
        {
            return false;
        }

        return !internalAssemblies.Contains(assemblyName);
    }
}
