using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Dependency;

internal static class ProjectDependenciesCollector
{
    private const string QudiRegistrationExtensionsName = "QudiRegistrations";
    private const string QudiGeneratedRegistrationsPrefix = "Qudi.Generated__";

    /// <summary>
    /// Collects project dependencies that contain Qudi registrations (heavy operation).
    /// </summary>
    public static EquatableArray<ProjectDependencyInfo> CollectDependencies(
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var dependencies = new List<ProjectDependencyInfo>();

        foreach (var reference in compilation.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            FindRegistrationExtensions(
                assembly,
                assembly.GlobalNamespace,
                dependencies,
                cancellationToken
            );
        }

        return new EquatableArray<ProjectDependencyInfo>(dependencies);
    }

    private static void FindRegistrationExtensions(
        IAssemblySymbol assemblySymbol,
        INamespaceSymbol namespaceSymbol,
        List<ProjectDependencyInfo> results,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Only traverse namespaces that can possibly contain Qudi.Generated__*.
        var namespaceName = namespaceSymbol.ToDisplayString();
        var isGlobal = namespaceSymbol.IsGlobalNamespace || string.IsNullOrEmpty(namespaceName);
        if (
            !isGlobal
            && !QudiGeneratedRegistrationsPrefix.StartsWith(namespaceName, StringComparison.Ordinal)
            && !namespaceName.StartsWith(QudiGeneratedRegistrationsPrefix, StringComparison.Ordinal)
        )
        {
            // Fast-exit for unrelated namespaces.
            return;
        }

        // Look for types named QudiRegistrationExtensions in namespaces
        // that start with Qudi.Generated__
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ns = type.ContainingNamespace.ToDisplayString();
            if (
                type.Name == QudiRegistrationExtensionsName
                && ns.StartsWith(QudiGeneratedRegistrationsPrefix, StringComparison.Ordinal)
            )
            {
                results.Add(
                    new ProjectDependencyInfo
                    {
                        AssemblyName = assemblySymbol.Name,
                        Namespace = ns,
                        ProjectHash = GenerateProjectHash(assemblySymbol, cancellationToken),
                    }
                );
            }
        }

        // Recurse into child namespaces (only when still relevant).
        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            FindRegistrationExtensions(assemblySymbol, child, results, cancellationToken);
        }
    }

    private static string GenerateProjectHash(
        IAssemblySymbol assemblySymbol,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FastHashGenerator.Generate(assemblySymbol.Identity.ToString(), 12);
    }

    // NOTE: We intentionally avoid path-based heuristics here because they are
    // environment-dependent and unreliable in real-world builds.
}
