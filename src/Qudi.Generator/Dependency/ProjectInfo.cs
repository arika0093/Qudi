using System;
using System.Collections.Generic;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Dependency;

internal record ProjectInfo
{
    /// <summary>
    /// The assembly name of the dependency.
    /// </summary>
    public required string AssemblyName { get; init; }

    /// <summary>
    /// Matching namespace for the dependency.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// A hash representing the state of the project.
    /// </summary>
    public required string ProjectHash { get; init; }

    /// <summary>
    /// Whether the dependency is usable for registration generation.
    /// </summary>
    public required Dictionary<Type, bool> AddServicesAvailable { get; init; } = [];

    /// <summary>
    /// The dependencies of the project.
    /// </summary>
    public EquatableArray<ProjectDependencyInfo> Dependencies { get; init; } = [];
}

internal record ProjectDependencyInfo
{
    /// <summary>
    /// The assembly name of the dependency.
    /// </summary>
    public required string AssemblyName { get; init; }

    /// <summary>
    /// Matching namespace for the dependency.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// A hash representing the state of the project.
    /// </summary>
    public required string ProjectHash { get; init; }
}
