using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Qudi.Visualizer;

public enum QudiVisualizationFormat
{
    Json,
    Dot,
    Mermaid,
    Dgml,
    Svg,
}

public sealed record QudiVisualizationFileOutput(string FilePath, QudiVisualizationFormat Format);

public sealed class QudiVisualizationOptions
{
    private readonly List<QudiVisualizationFileOutput> _outputs = [];
    private readonly List<Type> _traceServices = [];
    private readonly List<QudiVisualizationFormat> _outputFormats = [];

    public bool EnableConsoleOutput { get; set; } = true;

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

    public IReadOnlyCollection<QudiVisualizationFileOutput> Outputs => _outputs;

    public IReadOnlyCollection<Type> TraceServices => _traceServices;

    public QudiVisualizationOptions AddOutput(string filePath, QudiVisualizationFormat? format = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        var normalized = Path.GetFullPath(filePath);
        _outputs.Add(new QudiVisualizationFileOutput(normalized, format ?? InferFormatFromPath(normalized)));
        return this;
    }

    public QudiVisualizationOptions AddOutputs(params string[] filePaths)
    {
        foreach (var filePath in filePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            AddOutput(filePath);
        }

        return this;
    }

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

    public QudiVisualizationOptions SetOutputDirectory(string directoryPath, params QudiVisualizationFormat[] formats)
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

    internal QudiVisualizationRuntimeOptions BuildRuntimeOptions()
    {
        return new QudiVisualizationRuntimeOptions(
            EnableConsoleOutput,
            [.. _outputs],
            [.. _traceServices],
            GroupByNamespace,
            OutputDirectory,
            [.. _outputFormats]
        );
    }

    private static QudiVisualizationFormat InferFormatFromPath(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => QudiVisualizationFormat.Json,
            ".dot" or ".gv" => QudiVisualizationFormat.Dot,
            ".mmd" or ".mermaid" => QudiVisualizationFormat.Mermaid,
            ".dgml" => QudiVisualizationFormat.Dgml,
            ".svg" => QudiVisualizationFormat.Svg,
            _ => throw new InvalidOperationException(
                "Unable to infer format from extension. Use .json/.dot/.mmd/.dgml/.svg or specify format explicitly."
            ),
        };
    }
}

internal sealed record QudiVisualizationRuntimeOptions(
    bool EnableConsoleOutput,
    IReadOnlyCollection<QudiVisualizationFileOutput> Outputs,
    IReadOnlyCollection<Type> TraceServices,
    bool GroupByNamespace,
    string? OutputDirectory,
    IReadOnlyCollection<QudiVisualizationFormat> OutputFormats
);
