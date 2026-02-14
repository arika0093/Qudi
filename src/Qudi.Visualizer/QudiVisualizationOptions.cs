using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Qudi.Visualizer;

/// <summary>
/// Defines the output format for Qudi visualization. Supported formats include:
/// </summary>
public enum QudiVisualizationFormat
{
    /// <summary>
    /// JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// DOT format.
    /// </summary>
    Dot,

    /// <summary>
    /// Mermaid format.
    /// </summary>
    Mermaid,

    /// <summary>
    /// Markdown format.
    /// </summary>
    Markdown,

    /// <summary>
    /// DGML format.
    /// </summary>
    Svg,
}

/// <summary>
/// Controls which sections are rendered to the console output.
/// </summary>
[Flags]
public enum ConsoleDisplay
{
    /// <summary>
    /// No output to console.
    /// </summary>
    None = 0,

    /// <summary>
    /// Summary section with overall statistics and key insights.
    /// </summary>
    Summary = 1 << 0,

    /// <summary>
    /// Issues section with warnings about potential problems in the configuration, such as missing dependencies, circular dependencies, or services with multiple implementations.
    /// </summary>
    Issues = 1 << 1,

    /// <summary>
    /// List of all services and their dependencies. Can be turned on/off or set to auto (only show if number of services is below a certain threshold). Default is auto.
    /// </summary>
    ListAlways = 1 << 2,

    /// <summary>
    /// List of all services and their dependencies, but only if the total number of services is below a certain threshold (e.g. 20).
    /// If the number of services exceeds the threshold, the list will be hidden to avoid overwhelming the console output. Default is auto.
    /// </summary>
    ListAuto = 1 << 4,

    /// <summary>
    /// Tree view of service dependencies. This is a more compact representation that shows the hierarchy of services and their dependencies in a tree structure.
    /// It can be useful for visualizing complex configurations with many services and dependencies. Default is off.
    /// </summary>
    Tree = 1 << 5,

    /// <summary>
    /// Default console output. Summary | ListAuto | Issues.
    /// </summary>
    Default = Summary | ListAuto | Issues,

    /// <summary>
    /// All console output.
    /// </summary>
    All = Summary | Issues | ListAlways | Tree,
}

/// <summary>
/// Controls which sections are emitted via ILogger.
/// </summary>
[Flags]
public enum LoggerOutput
{
    /// <summary>
    /// No output to logger.
    /// </summary>
    None = 0,

    /// <summary>
    /// Summary section with overall statistics and key insights.
    /// </summary>
    Summary = 1 << 0,

    /// <summary>
    /// Warnings about potential issues in the configuration, such as missing dependencies, circular dependencies, or services with multiple implementations.
    /// </summary>
    List = 1 << 1,

    /// <summary>
    /// Warnings about potential issues in the configuration, such as missing dependencies, circular dependencies, or services with multiple implementations.
    /// </summary>
    Issues = 1 << 2,

    /// <summary>
    /// Default logger output. Summary | Issues.
    /// </summary>
    Default = Summary | Issues,
}

/// <summary>
/// Represents an output file for Qudi visualization, including the file path and format.
/// </summary>
public sealed record QudiVisualizationFileOutput(string FilePath, QudiVisualizationFormat Format);

/// <summary>
/// Options for configuring Qudi visualization output.
/// This class provides a fluent API for specifying various output options,
/// such as enabling console output, adding file outputs, specifying services to trace, and configuring output directories and formats for exported individual graphs.
/// </summary>
public sealed class QudiVisualizationOptions
{
    private readonly List<QudiVisualizationFileOutput> _outputs = [];
    private readonly List<Type> _traceServices = [];
    private readonly List<QudiVisualizationFormat> _outputFormats = [];
    private ConsoleDisplay _consoleOutput = ConsoleDisplay.Default;

    /// <summary>
    /// Enable console output of visualization results. Use <see cref="ConsoleOutput"/> for fine-grained control.
    /// </summary>
    public bool EnableConsoleOutput
    {
        get => _consoleOutput != ConsoleDisplay.None;
        set => _consoleOutput = value ? ConsoleDisplay.Default : ConsoleDisplay.None;
    }

    /// <summary>
    /// Controls which sections are rendered to the console output.
    /// Default is <see cref="ConsoleDisplay.Default"/>.
    /// </summary>
    public ConsoleDisplay ConsoleOutput
    {
        get => _consoleOutput;
        set => _consoleOutput = value;
    }

    /// <summary>
    /// Controls which sections are emitted via ILogger. Default is <see cref="LoggerOutput.Default"/>.
    /// </summary>
    public LoggerOutput LoggerOutput { get; set; } = LoggerOutput.Default;

    /// <summary>
    /// Optional logger factory for ILogger output.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Enable grouping by namespace using subgraph in Mermaid output.
    /// Default is false.
    /// </summary>
    public bool GroupByNamespace { get; set; } = false;

    /// <summary>
    /// Output directory for exported individual graphs.
    /// Used when Export attribute is present on types.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Output formats for exported individual graphs.
    /// </summary>
    public IReadOnlyCollection<QudiVisualizationFormat> OutputFormats => _outputFormats;

    /// <summary>
    /// Collection of file outputs to write the visualization report to. Each output includes a file path and format.
    /// </summary>
    public IReadOnlyCollection<QudiVisualizationFileOutput> Outputs => _outputs;

    /// <summary>
    /// Collection of service types to trace in the visualization. If empty, all services will be traced.
    /// </summary>
    public IReadOnlyCollection<Type> TraceServices => _traceServices;

    /// <summary>
    /// Adds an output file for the visualization report. The format can be inferred from the file extension or specified explicitly.
    /// </summary>
    /// <param name="filePath"> The file path to write the visualization report to. Supported extensions include .json, .dot/.gv, .mmd/.mermaid, .md, .dgml, and .svg.</param>
    /// <param name="format"> Optional format to use for the output. If not specified, the format will be inferred from the file extension.</param>
    public QudiVisualizationOptions AddOutput(
        string filePath,
        QudiVisualizationFormat? format = null
    )
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        var normalized = Path.GetFullPath(filePath);
        var ext = VisualizeFormatConvertExtensions.DetermineFromFilePath(normalized);

        _outputs.Add(new QudiVisualizationFileOutput(normalized, format ?? ext));
        return this;
    }

    /// <summary>
    /// Adds multiple output files for the visualization report. The format for each file can be inferred from the file extension or specified explicitly.
    /// </summary>
    /// <param name="filePaths"> The file paths to write the visualization report to. Supported extensions include .json, .dot/.gv, .mmd/.mermaid, .md, .dgml, and .svg.</param>
    public QudiVisualizationOptions AddOutputs(params string[] filePaths)
    {
        foreach (var filePath in filePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            AddOutput(filePath);
        }

        return this;
    }

    /// <summary>
    /// Adds a service type to trace in the visualization. If no services are added, all services will be traced by default.
    /// </summary>
    /// <param name="serviceType"> The service type to trace in the visualization.</param>
    public QudiVisualizationOptions AddTraceService(Type serviceType)
    {
        if (serviceType is null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }

        if (!_traceServices.Contains(serviceType))
        {
            _traceServices.Add(serviceType);
        }
        return this;
    }

    /// <summary>
    /// Sets the output directory for exported individual graphs.
    /// This is used when the Export attribute is present on types.
    /// Optionally, you can specify the output formats to use for these exported graphs.
    /// If no formats are specified, it will default to Mermaid format.
    /// </summary>
    /// <param name="directoryPath"> The directory path to write the exported individual graphs to.</param>
    /// <param name="formats"> Optional formats to use for the exported individual graphs. If not specified, it will default to Mermaid format.</param>
    public QudiVisualizationOptions SetOutputDirectory(
        string directoryPath,
        params QudiVisualizationFormat[] formats
    )
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        }

        OutputDirectory = Path.GetFullPath(directoryPath);
        _outputFormats.Clear();
        if (formats.Length > 0)
        {
            _outputFormats.AddRange(formats);
        }
        else
        {
            // Default to all formats if none specified
            _outputFormats.Add(QudiVisualizationFormat.Mermaid);
        }
        return this;
    }

    // internal method to build runtime options from the configured properties
    internal QudiVisualizationRuntimeOptions BuildRuntimeOptions()
    {
        return new QudiVisualizationRuntimeOptions(
            ConsoleOutput,
            LoggerOutput,
            LoggerFactory,
            [.. _outputs],
            [.. _traceServices],
            GroupByNamespace,
            OutputDirectory,
            [.. _outputFormats]
        );
    }
}

internal static class VisualizeFormatConvertExtensions
{
    // Helper method to get file extension for a given format
    public static string ToExtension(this QudiVisualizationFormat format)
    {
        return format switch
        {
            QudiVisualizationFormat.Json => "json",
            QudiVisualizationFormat.Dot => "dot",
            QudiVisualizationFormat.Mermaid => "mmd",
            QudiVisualizationFormat.Markdown => "md",
            QudiVisualizationFormat.Svg => "svg",
            _ => throw new InvalidOperationException("Unsupported visualization format."),
        };
    }

    // Helper method to determine format from file extension
    public static QudiVisualizationFormat DetermineFromFilePath(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => QudiVisualizationFormat.Json,
            ".dot" or ".gv" => QudiVisualizationFormat.Dot,
            ".mmd" or ".mermaid" => QudiVisualizationFormat.Mermaid,
            ".md" => QudiVisualizationFormat.Markdown,
            ".svg" => QudiVisualizationFormat.Svg,
            _ => throw new InvalidOperationException(
                "Unable to infer format from extension. Use .json/.dot/.mmd/.md/.dgml/.svg or specify format explicitly."
            ),
        };
    }
}

internal sealed record QudiVisualizationRuntimeOptions(
    ConsoleDisplay ConsoleOutput,
    LoggerOutput LoggerOutput,
    ILoggerFactory? LoggerFactory,
    IReadOnlyCollection<QudiVisualizationFileOutput> Outputs,
    IReadOnlyCollection<Type> TraceServices,
    bool GroupByNamespace,
    string? OutputDirectory,
    IReadOnlyCollection<QudiVisualizationFormat> OutputFormats
);
