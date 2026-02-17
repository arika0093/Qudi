using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Container;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Dependency;

/// <summary>
/// Coordinates project information and dependency collection for Qudi source generation.
/// </summary>
internal static class DependsCollector
{
    /// <summary>
    /// Collects basic project information (lightweight operation).
    /// </summary>
    public static IncrementalValueProvider<ProjectBasicInfo> QudiProjectBasicInfo(
        IncrementalGeneratorInitializationContext context
    )
    {
        return context
            .CompilationProvider.Select(
                static (compilation, cancellationToken) =>
                    ProjectBasicInfoCollector.CollectBasicInfo(compilation, cancellationToken)
            )
            .WithComparer(EqualityComparer<ProjectBasicInfo>.Default);
    }

    /// <summary>
    /// Collects project dependencies that contain Qudi registrations (heavy operation).
    /// </summary>
    public static IncrementalValueProvider<
        EquatableArray<ProjectDependencyInfo>
    > QudiProjectDependencies(IncrementalGeneratorInitializationContext context)
    {
        return context
            .CompilationProvider.Select(
                static (compilation, cancellationToken) =>
                    ProjectDependenciesCollector.CollectDependencies(compilation, cancellationToken)
            )
            .WithComparer(EqualityComparer<EquatableArray<ProjectDependencyInfo>>.Default);
    }

    /// <summary>
    /// Combines basic project information and dependencies into a single ProjectInfo object.
    /// </summary>
    public static IncrementalValueProvider<ProjectInfo> QudiProjectInfo(
        IncrementalGeneratorInitializationContext context
    )
    {
        var basicInfo = QudiProjectBasicInfo(context);
        var dependencies = QudiProjectDependencies(context);

        return basicInfo
            .Combine(dependencies)
            .Select(
                static (combined, _) =>
                {
                    var (basic, deps) = combined;
                    return new ProjectInfo { Basic = basic, Dependencies = deps };
                }
            )
            .WithComparer(EqualityComparer<ProjectInfo>.Default);
    }
}
