using System;
using System.Collections.Generic;
using System.Linq;
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
            if (reference is CompilationReference)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    FindRegistrationExtensions(
                        assembly,
                        assembly.GlobalNamespace,
                        dependencies,
                        cancellationToken
                    );
                }

                continue;
            }

            if (
                reference is PortableExecutableReference portable
                && IsProjectReferencePath(portable.FilePath)
                && compilation.GetAssemblyOrModuleSymbol(reference)
                    is IAssemblySymbol projectAssembly
            )
            {
                FindRegistrationExtensions(
                    projectAssembly,
                    projectAssembly.GlobalNamespace,
                    dependencies,
                    cancellationToken
                );
            }
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
        // Look for types named QudiRegistrationExtensions in namespaces
        // that start with Qudi.Generated.Registrations__
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
        // Recurse into child namespaces
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

    private static bool IsProjectReferencePath(string? filePath)
    {
        // TODO: The project determination needs to be more precise. The current implementation is path-dependent and thus unreliable.
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }
        var path = filePath!.Replace('\\', '/');
        if (ExcludePath.Any(exclude => path.Contains(exclude, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        if (IncludePath.Any(include => path.Contains(include, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return false;
    }

    private static readonly IReadOnlyCollection<string> ExcludePath =
    [
        "/.nuget/",
        "/packages/",
        "/packs/",
    ];

    private static readonly IReadOnlyCollection<string> IncludePath = ["/bin/", "/obj/"];
}
