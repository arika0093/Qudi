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

internal static class DependsCollector
{
    private const string QudiRegistrationExtensionsName = "QudiRegistrations";
    private const string QudiGeneratedRegistrationsPrefix = "Qudi.Generated__";

    /// <summary>
    /// Collects project dependencies that contain Qudi registrations.
    /// </summary>
    public static IncrementalValueProvider<ProjectInfo> QudiProjectDependencies(
        IncrementalGeneratorInitializationContext context
    )
    {
        return context
            .CompilationProvider.Select(
                static (compilation, cancellationToken) =>
                    CollectProjectInfo(compilation, cancellationToken)
            )
            .WithComparer(EqualityComparer<ProjectInfo>.Default);
    }

    /// <summary>
    /// Collects project info with dependencies that contain Qudi registrations.
    /// </summary>
    public static ProjectInfo CollectProjectInfo(
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        // fetch dependencies
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
        var self = new ProjectInfo
        {
            AssemblyName = compilation.AssemblyName ?? "UnknownAssembly",
            Namespace = compilation.Assembly.GlobalNamespace.ToDisplayString(),
            ProjectHash = GenerateProjectHash(compilation.Assembly, cancellationToken),
            AddServicesAvailable = AddServiceSupportChecker.IsSupported(compilation),
            Dependencies = new EquatableArray<ProjectDependencyInfo>(dependencies),
        };
        return self;
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
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }
        var path = filePath!.ToLowerInvariant();
        if (ExcludePath.Any(exclude => path.Contains(exclude)))
        {
            return false;
        }
        if (IncludePath.Any(include => path.Contains(include)))
        {
            return true;
        }
        return false;
    }

    private static readonly IReadOnlyCollection<string> ExcludePath =
    [
        "\\.nuget\\",
        "\\packages\\",
        "\\packs\\",
    ];

    private static readonly IReadOnlyCollection<string> IncludePath = ["\\bin\\", "\\obj\\"];
}
