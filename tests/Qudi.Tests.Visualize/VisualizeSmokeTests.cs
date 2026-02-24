using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Qudi.Visualizer;
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
                opt.ConsoleOutput = ConsoleDisplay.None;
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

    [Test]
    public void HandlesVisualizationOutputWarnings()
    {
        // This test verifies that the visualizer gracefully handles scenarios that may produce warnings,
        // such as SVG generation when Graphviz is not available.
        var rootDir = Path.Combine(
            Path.GetTempPath(),
            "Qudi.Visualize.Tests",
            Guid.NewGuid().ToString("N")
        );
        var svgOutputPath = Path.Combine(rootDir, "visualization.svg");

        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.EnableVisualizationOutput(opt =>
            {
                opt.ConsoleOutput = ConsoleDisplay.None;
                opt.AddOutput(svgOutputPath);
            });
        });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<VisualizeWarningRoot>();

        // When SVG output is requested, it should always create a .dot file
        // (either as the source for SVG generation, or as a fallback if graphviz is not available)
        var dotPath = Path.ChangeExtension(svgOutputPath, ".dot");
        File.Exists(dotPath).ShouldBeTrue();

        // The .dot file should contain valid DOT format content
        var dotContent = File.ReadAllText(dotPath);
        dotContent.ShouldContain("digraph");
    }

    [Test]
    public void AppliesDotAndMermaidLayoutOptions()
    {
        var rootDir = Path.Combine(
            Path.GetTempPath(),
            "Qudi.Visualize.Tests",
            Guid.NewGuid().ToString("N")
        );
        var dotOutputPath = Path.Combine(rootDir, "visualization.dot");
        var mermaidOutputPath = Path.Combine(rootDir, "visualization.mmd");

        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.EnableVisualizationOutput(opt =>
            {
                opt.ConsoleOutput = ConsoleDisplay.None;
                opt.GraphDirection = QudiVisualizationDirection.TopToBottom;
                opt.FontFamily = "JetBrains Mono";
                opt.AddOutput(dotOutputPath);
                opt.AddOutput(mermaidOutputPath);
            });
        });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<VisualizeRoot>();

        var dotContent = File.ReadAllText(dotOutputPath);
        dotContent.ShouldContain("rankdir=TB;");
        dotContent.ShouldContain("node [shape=box, fontname=\"JetBrains Mono\"];");
        dotContent.ShouldNotContain("shape=ellipse");

        var mermaidContent = File.ReadAllText(mermaidOutputPath);
        mermaidContent.ShouldContain("flowchart TB");
        mermaidContent.ShouldContain("fontFamily': 'JetBrains Mono'");
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

[DITransient(Export = true)]
internal sealed class VisualizeWarningRoot
{
    public VisualizeWarningRoot(VisualizeWarningDependency dependency)
    {
        Dependency = dependency;
    }

    public VisualizeWarningDependency Dependency { get; }
}

[DITransient]
internal sealed class VisualizeWarningDependency;
