using System;
using System.Collections.Generic;
using Spectre.Console;

namespace Qudi.Visualizer;

internal class QudiVisualizationConsoleRenderer(IAnsiConsole AnsiConsole)
{
    public void Render(QudiVisualizationReport report, IReadOnlyList<string> warnings)
    {
        AnsiConsole.MarkupLine("[bold]Qudi Visualization[/]");

        RenderSummary(report.Summary);
        RenderRegistrations(report.Registrations);
        RenderMissing(report.MissingRegistrations);
        RenderCycles(report.Cycles);
        RenderMultiples(report.MultipleRegistrations);
        RenderLifetimeWarnings(report.LifetimeWarnings);
        RenderTraces(report.Traces);
        RenderWarnings(warnings);
    }

    private void RenderSummary(QudiVisualizationSummary summary)
    {
        var table = new Table().AddColumn("Metric").AddColumn("Count");
        table.AddRow("Registrations", summary.RegistrationCount.ToString());
        table.AddRow("Missing", summary.MissingCount.ToString());
        table.AddRow("Cycles", summary.CycleCount.ToString());
        table.AddRow("Multiple", summary.MultipleRegistrationCount.ToString());
        table.AddRow("Lifetime warnings", summary.LifetimeWarningCount.ToString());
        AnsiConsole.Write(table);
    }

    private void RenderRegistrations(IReadOnlyList<QudiRegistrationTableRow> rows)
    {
        var table = new Table()
            .AddColumn("Service")
            .AddColumn("Impl")
            .AddColumn("Lifetime")
            .AddColumn("Key")
            .AddColumn("When")
            .AddColumn("Order")
            .AddColumn("Decorator");

        foreach (var row in rows)
        {
            table.AddRow(
                Markup.Escape(row.Service),
                Markup.Escape(row.Implementation),
                Markup.Escape(row.Lifetime),
                Markup.Escape(row.Key),
                Markup.Escape(row.When),
                row.Order.ToString(),
                row.Decorator ? "Yes" : "No"
            );
        }

        AnsiConsole.Write(table);
    }

    private void RenderMissing(IReadOnlyList<QudiMissingRegistration> missing)
    {
        if (missing.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Missing registrations[/]");
        var table = new Table().AddColumn("Required").AddColumn("Requested by");
        foreach (var item in missing)
        {
            table.AddRow(Markup.Escape(item.RequiredType), Markup.Escape(item.RequestedBy));
        }
        AnsiConsole.Write(table);
    }

    private void RenderCycles(IReadOnlyList<QudiCycle> cycles)
    {
        if (cycles.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Cycles[/]");
        var table = new Table().AddColumn("Path");
        foreach (var cycle in cycles)
        {
            table.AddRow(Markup.Escape(string.Join(" -> ", cycle.Path)));
        }
        AnsiConsole.Write(table);
    }

    private void RenderMultiples(IReadOnlyList<QudiMultipleRegistration> multiples)
    {
        if (multiples.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Multiple registrations[/]");
        var table = new Table().AddColumn("Service").AddColumn("Key").AddColumn("Count");
        foreach (var item in multiples)
        {
            table.AddRow(
                Markup.Escape(item.Service),
                Markup.Escape(item.Key),
                item.Count.ToString()
            );
        }
        AnsiConsole.Write(table);
    }

    private void RenderLifetimeWarnings(IReadOnlyList<QudiLifetimeWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Lifetime warnings[/]");
        var table = new Table()
            .AddColumn("Service")
            .AddColumn("From")
            .AddColumn("To")
            .AddColumn("Message");
        foreach (var item in warnings)
        {
            table.AddRow(
                Markup.Escape(item.Service),
                Markup.Escape(item.From),
                Markup.Escape(item.To),
                Markup.Escape(item.Message)
            );
        }
        AnsiConsole.Write(table);
    }

    private void RenderTraces(IReadOnlyList<QudiTraceResult> traces)
    {
        if (traces.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Resolution traces[/]");
        foreach (var trace in traces)
        {
            var root = new Tree(Markup.Escape(trace.Service));
            foreach (var node in trace.Roots)
            {
                AddTraceNode(root, node);
            }
            AnsiConsole.Write(root);
        }
    }

    private void AddTraceNode(IHasTreeNodes parent, QudiTraceNode node)
    {
        var label = node.Label;
        if (node.IsMissing)
        {
            label += " (missing)";
        }
        if (node.IsCycle)
        {
            label += " (cycle)";
        }
        if (!string.IsNullOrWhiteSpace(node.Detail))
        {
            label += " - " + node.Detail;
        }

        var child = parent.AddNode(Markup.Escape(label));
        foreach (var nested in node.Children)
        {
            AddTraceNode(child, nested);
        }
    }

    private void RenderWarnings(IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("[bold]Visualizer warnings[/]");
        foreach (var warning in warnings)
        {
            AnsiConsole.MarkupLine("- " + Markup.Escape(warning));
        }
    }
}
