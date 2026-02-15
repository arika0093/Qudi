using System;
using System.Linq;
using TUnit;

namespace Qudi.Tests;

public sealed class VisualizationGraphBuilderTests
{
    [Test]
    public void Build_CreatesRegistrationAndDependencyEdges()
    {
        var configuration = CreateConfiguration();

        var graph = QudiVisualizationGraphBuilder.Build(configuration);

        graph.Nodes.Any(n => n.Label == nameof(IRootService) && n.Kind == "service").ShouldBeTrue();
        graph.Nodes.Any(n => n.Label == nameof(RootService) && n.Kind == "implementation").ShouldBeTrue();
        graph.Nodes.Any(n => n.Label == nameof(IDependency) && n.Kind == "service").ShouldBeTrue();

        graph
            .Edges.Any(e =>
                e.Kind == "registration"
                && graph.Nodes.Any(n => n.Id == e.From && n.Label == nameof(IRootService))
                && graph.Nodes.Any(n => n.Id == e.To && n.Label == nameof(RootService))
            )
            .ShouldBeTrue();

        graph
            .Edges.Any(e =>
                e.Kind == "dependency"
                && graph.Nodes.Any(n => n.Id == e.From && n.Label == nameof(RootService))
                && graph.Nodes.Any(n => n.Id == e.To && n.Label == nameof(IDependency))
            )
            .ShouldBeTrue();
    }

    [Test]
    public void BuildFromRoot_ReturnsOnlyReachableNodes()
    {
        var configuration = CreateConfiguration();

        var graph = QudiVisualizationGraphBuilder.BuildFromRoot(configuration, typeof(IRootService));

        graph.Nodes.Any(n => n.Label == nameof(IRootService)).ShouldBeTrue();
        graph.Nodes.Any(n => n.Label == nameof(RootService)).ShouldBeTrue();
        graph.Nodes.Any(n => n.Label == nameof(IDependency)).ShouldBeTrue();
        graph.Nodes.Any(n => n.Label == nameof(UnusedService)).ShouldBeFalse();
    }

    private static QudiConfiguration CreateConfiguration()
    {
        var registrations = new TypeRegistrationInfo[]
        {
            new()
            {
                Type = typeof(RootService),
                Lifetime = Lifetime.Singleton,
                AsTypes = [typeof(IRootService)],
                RequiredTypes = [typeof(IDependency)],
                AssemblyName = typeof(RootService).Assembly.GetName().Name ?? string.Empty,
            },
            new()
            {
                Type = typeof(Dependency),
                Lifetime = Lifetime.Transient,
                AsTypes = [typeof(IDependency)],
                AssemblyName = typeof(Dependency).Assembly.GetName().Name ?? string.Empty,
            },
            new()
            {
                Type = typeof(UnusedService),
                Lifetime = Lifetime.Transient,
                AsTypes = [typeof(IUnusedService)],
                AssemblyName = typeof(UnusedService).Assembly.GetName().Name ?? string.Empty,
            },
        };

        return new QudiConfiguration { Registrations = registrations, Conditions = [] };
    }

    public interface IRootService;
    public sealed class RootService(IDependency dependency) : IRootService;

    public interface IDependency;
    public sealed class Dependency : IDependency;

    public interface IUnusedService;
    public sealed class UnusedService : IUnusedService;
}
