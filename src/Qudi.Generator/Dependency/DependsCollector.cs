using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        return context.CompilationProvider
            .Select(
                // TODO: use cancellation token and check for cancellation anytime!!
                static (compilation, _) => CollectProjectInfo(compilation)
            )
            // TODO: Add WithComparer for caching
            ;
    }

    /// <summary>
    /// Collects project info with dependencies that contain Qudi registrations.
    /// </summary>
    public static ProjectInfo CollectProjectInfo(Compilation compilation)
    {
        // fetch dependencies
        var dependencies = new List<ProjectDependencyInfo>();
        foreach (var reference in compilation.References)
        {
            if (reference is CompilationReference)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    FindRegistrationExtensions(assembly, assembly.GlobalNamespace, dependencies);
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
                    dependencies
                );
            }
        }
        var self = new ProjectInfo
        {
            AssemblyName = compilation.AssemblyName ?? "UnknownAssembly",
            Namespace = compilation.Assembly.GlobalNamespace.ToDisplayString(),
            ProjectHash = GenerateProjectHash(compilation.Assembly),
            AddServicesAvailable = AddServiceSupportChecker.IsSupported(compilation),
            Dependencies = new EquatableArray<ProjectDependencyInfo>(dependencies),
        };
        return self;
    }

    private static void FindRegistrationExtensions(
        IAssemblySymbol assemblySymbol,
        INamespaceSymbol namespaceSymbol,
        List<ProjectDependencyInfo> results
    )
    {
        // Look for types named QudiRegistrationExtensions in namespaces
        // that start with Qudi.Generated.Registrations__
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
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
                        ProjectHash = GenerateProjectHash(assemblySymbol),
                    }
                );
            }
        }
        // Recurse into child namespaces
        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            FindRegistrationExtensions(assemblySymbol, child, results);
        }
    }

    private static string GenerateProjectHash(IAssemblySymbol assemblySymbol)
    {
        // TODO: use xxHash(XXH3) for better performance!!
        using var sha256 = SHA256.Create();
        // hash based on assembly identity and global namespace
        var input = $"{assemblySymbol.Identity}-{assemblySymbol.GlobalNamespace.ToDisplayString()}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        // and get as hex string (8 characters)
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8);
    }

    private static bool IsProjectReferencePath(string? filePath)
    {
        // TODO: Is there a better way to identify? For example, using symbol information.
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var normalized = filePath!.Replace('/', '\\');
        if (
            normalized.Contains("\\.nuget\\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("\\packages\\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("\\packs\\", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return normalized.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase);
    }
}

