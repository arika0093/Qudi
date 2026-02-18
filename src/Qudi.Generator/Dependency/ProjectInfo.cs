using System;
using System.Collections.Generic;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Dependency;

internal record ProjectBasicInfo
{
    /// <summary>
    /// The assembly name of the project.
    /// </summary>
    public required string AssemblyName { get; init; }

    /// <summary>
    /// Matching namespace for the project.
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// A hash representing the state of the project.
    /// </summary>
    public required string ProjectHash { get; init; }

    /// <summary>
    /// Whether the project is usable for registration generation.
    /// </summary>
    public required Dictionary<Type, bool> AddServicesAvailable { get; init; } = [];
}

/// <summary>
/// A simple wrapper combining basic project information and dependencies.
/// </summary>
internal record ProjectInfo
{
    /// <summary>
    /// Basic project information.
    /// </summary>
    public required ProjectBasicInfo Basic { get; init; }

    /// <summary>
    /// Project dependencies.
    /// </summary>
    public required EquatableArray<ProjectDependencyInfo> Dependencies { get; init; }
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
