using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Qudi.Generator.Container;
using Qudi.Generator.Utility;

namespace Qudi.Generator.Dependency;

internal static class ProjectBasicInfoCollector
{
    /// <summary>
    /// Collects basic project information (lightweight operation).
    /// </summary>
    public static ProjectBasicInfo CollectBasicInfo(
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        return new ProjectBasicInfo
        {
            AssemblyName = compilation.AssemblyName ?? "UnknownAssembly",
            Namespace = compilation.Assembly.GlobalNamespace.ToDisplayString(),
            ProjectHash = GenerateProjectHash(compilation.Assembly, cancellationToken),
            AddServicesAvailable = AddServiceSupportChecker.IsSupported(compilation),
        };
    }

    private static string GenerateProjectHash(
        IAssemblySymbol assemblySymbol,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return FastHashGenerator.Generate(assemblySymbol.Identity.ToString(), 12);
    }
}
