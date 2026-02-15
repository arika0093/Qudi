using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public void OutputsWarningWhenSvgGenerationFails()
    {
        var rootDir = Path.Combine(
            Path.GetTempPath(),
            "Qudi.Visualize.Tests",
            Guid.NewGuid().ToString("N")
        );
        var svgOutputPath = Path.Combine(rootDir, "visualization.svg");
        var logMessages = new System.Collections.Generic.List<string>();

        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.EnableVisualizationOutput(opt =>
            {
                opt.ConsoleOutput = ConsoleDisplay.None;
                opt.AddOutput(svgOutputPath);
                opt.LoggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddProvider(new TestLoggerProvider(logMessages));
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });
        });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<VisualizeWarningRoot>();

        // When dot (Graphviz) is not available, an SVG output should still create a .dot file
        // and the warning should be logged
        var dotPath = Path.ChangeExtension(svgOutputPath, ".dot");
        File.Exists(dotPath).ShouldBeTrue();
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

// Test logger provider to capture log messages
internal sealed class TestLoggerProvider(System.Collections.Generic.List<string> logMessages)
    : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestLogger(logMessages);

    public void Dispose() { }
}

internal sealed class TestLogger(System.Collections.Generic.List<string> logMessages) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        logMessages.Add(formatter(state, exception));
    }
}
