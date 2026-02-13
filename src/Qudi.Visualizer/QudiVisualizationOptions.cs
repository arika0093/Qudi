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

    public bool EnableConsoleOutput { get; set; } = true;

    /// <summary>
    /// Enable grouping by namespace using subgraph in Mermaid output.
    /// Default is false.
    /// </summary>
    public bool GroupByNamespace { get; set; } = false;

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

    internal QudiVisualizationRuntimeOptions BuildRuntimeOptions()
    {
        return new QudiVisualizationRuntimeOptions(
            EnableConsoleOutput,
            [.. _outputs],
            [.. _traceServices],
            GroupByNamespace
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
    bool GroupByNamespace
);
