using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests.Visualize;

public sealed class VisualizeSmokeTests
{
    [Test]
    public void WritesVisualizationOutputs()
    {
        var rootDir = Path.Combine(
            Path.GetTempPath(),
            "Qudi.Visualize.Tests",
            Guid.NewGuid().ToString("N")
        );
        var outputPath = Path.Combine(rootDir, "visualization.md");
        var exportDir = Path.Combine(rootDir, "exported");

        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.EnableVisualizationOutput(opt =>
            {
                opt.AddOutput(outputPath);
                opt.SetOutputDirectory(exportDir, QudiVisualizationFormat.Mermaid);
            });
        });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<VisualizeRoot>();

        File.Exists(outputPath).ShouldBeTrue();
        Directory.Exists(exportDir).ShouldBeTrue();
        Directory.GetFiles(exportDir, "*.mmd").Length.ShouldBeGreaterThan(0);
        File.ReadAllText(outputPath).ShouldContain("mermaid");
    }
}

[DITransient(Export = true)]
internal sealed class VisualizeRoot
{
    public VisualizeRoot(VisualizeDependency dependency)
    {
        Dependency = dependency;
    }

    public VisualizeDependency Dependency { get; }
}

[DITransient]
internal sealed class VisualizeDependency;
