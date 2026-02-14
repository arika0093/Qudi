using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;

namespace Qudi.Visualizer;

internal class QudiVisualizationConsoleRenderer(IAnsiConsole AnsiConsole)
{
    private const int RegistrationListAutoHideThreshold = 30;

    public void Render(
        QudiVisualizationReport report,
        IReadOnlyList<string> warnings,
        ConsoleDisplay display
    )
    {
        if (display == ConsoleDisplay.None)
        {
            return;
        }
        // Header with logo
        var rule = new Rule("[bold cyan]識 Qudi Dependency Injection Visualization[/]")
        {
            Justification = Justify.Center,
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        // Summary metrics
        if (IsEnabled(display, ConsoleDisplay.Summary))
        {
            AnsiConsole.Write(CreateSummaryPanel(report.Summary));
            AnsiConsole.WriteLine();
        }

        // Registrations table
        var listDisplay = ResolveRegistrationListDisplay(display);
        if (listDisplay != RegistrationListDisplay.None)
        {
            if (
                listDisplay == RegistrationListDisplay.On
                || (
                    listDisplay == RegistrationListDisplay.Auto
                    && report.Registrations.Count <= RegistrationListAutoHideThreshold
                )
            )
            {
                AnsiConsole.Write(CreateRegistrationsPanel(report.Registrations));
                AnsiConsole.WriteLine();
            }
            else if (listDisplay == RegistrationListDisplay.Auto)
            {
                AnsiConsole.Write(CreateRegistrationsAutoHiddenPanel(report.Registrations.Count));
                AnsiConsole.WriteLine();
            }
        }

        // Issues panel (fixed height, no vertical expansion)
        if (IsEnabled(display, ConsoleDisplay.Issues))
        {
            AnsiConsole.Write(
                CreateIssuesPanel(
                    report.MissingRegistrations,
                    report.Cycles,
                    report.LifetimeWarnings
                )
            );
            AnsiConsole.WriteLine();
        }

        var showTraces = IsEnabled(display, ConsoleDisplay.Traces);
        var showWarnings = IsEnabled(display, ConsoleDisplay.Warnings);

        // Footer: Traces and Warnings
        if (showTraces && showWarnings)
        {
            var footerLayout = new Layout("Footer").SplitColumns(
                new Layout("Traces").Ratio(2),
                new Layout("Warnings").Ratio(1)
            );

            footerLayout["Traces"].Update(CreateTracesPanel(report.Traces));
            footerLayout["Warnings"].Update(CreateWarningsPanel(warnings));

            AnsiConsole.Write(footerLayout);
        }
        else if (showTraces)
        {
            AnsiConsole.Write(CreateTracesPanel(report.Traces));
        }
        else if (showWarnings)
        {
            AnsiConsole.Write(CreateWarningsPanel(warnings));
        }
    }

    private Panel CreateSummaryPanel(QudiVisualizationSummary summary)
    {
        var grid = new Grid()
            .Alignment(Justify.Center)
            .AddColumn(new GridColumn().Alignment(Justify.Right).PadRight(4))
            .AddColumn(new GridColumn().Alignment(Justify.Right).PadRight(4))
            .AddColumn(new GridColumn().Alignment(Justify.Right).PadRight(4))
            .AddColumn(new GridColumn().Alignment(Justify.Right).PadRight(4));

        // Row 1: Labels
        grid.AddRow(
            new Markup("[bold green]投 Registrations[/]"),
            new Markup("[bold red]笶・Missing[/]"),
            new Markup("[bold magenta]売 Cycles[/]"),
            new Markup("[bold orange1]庁 Lifetime[/]")
        );

        // Row 2: Values with color coding
        grid.AddRow(
            new Markup($"[bold green]{summary.RegistrationCount}[/]"),
            new Markup(
                summary.MissingCount > 0
                    ? $"[bold red]{summary.MissingCount}[/]"
                    : $"[dim]{summary.MissingCount}[/]"
            ),
            new Markup(
                summary.CycleCount > 0
                    ? $"[bold red]{summary.CycleCount}[/]"
                    : $"[dim]{summary.CycleCount}[/]"
            ),
            new Markup(
                summary.LifetimeWarningCount > 0
                    ? $"[bold orange1]{summary.LifetimeWarningCount}[/]"
                    : $"[dim]{summary.LifetimeWarningCount}[/]"
            )
        );

        return new Panel(grid)
        {
            Header = new PanelHeader("[bold]嶋 Summary Metrics[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Expand = true,
        };
    }

    private Panel CreateRegistrationsPanel(IReadOnlyList<QudiRegistrationTableRow> rows)
    {
        const string Nothing = "[dim]*[/]";

        var table = new Table()
            .Border(TableBorder.Simple)
            .BorderColor(Color.Green)
            .Expand()
            .AddColumn(new TableColumn("[bold cyan]Service[/]"))
            .AddColumn(new TableColumn("[bold green]Implementation[/]"))
            .AddColumn(new TableColumn("[bold yellow]Life[/]").Centered())
            .AddColumn(new TableColumn("[bold blue]Cond[/]"))
            .AddColumn(new TableColumn("[bold magenta]Key[/]"))
            .AddColumn(new TableColumn("[bold orange1]Order[/]"));

        foreach (var row in rows)
        {
            var lifetimeIcon = row.Lifetime switch
            {
                "Singleton" => "白",
                "Scoped" => "逃",
                "Transient" => "笞｡",
                _ => row.Lifetime,
            };

            // Combine condition info - keep it concise
            var condition = "";
            if (row.When == "*")
            {
                condition = Nothing;
            }
            else
            {
                // Shorten condition names
                var whenText = row.When.Replace("Development", "Dev").Replace("Production", "Prod");
                condition = $"[blue]{whenText}[/]";
            }

            var key = row.Key != "-" ? $"[magenta]{row.Key}[/]" : Nothing;

            var orderText = row.Order switch
            {
                < 0 => $"[red]{row.Order}[/]",
                0 => Nothing,
                > 0 => $"[yellow]{row.Order}[/]",
            };

            var serviceColor = row.Service == row.Implementation ? "green" : "cyan";
            var implColor = row.Decorator ? "red" : "green";

            table.AddRow(
                $"[{serviceColor}]{Markup.Escape(row.Service)}[/]",
                $"[{implColor}]{Markup.Escape(row.Implementation)}[/]",
                $"[yellow]{lifetimeIcon}[/]",
                condition,
                key,
                orderText
            );
        }

        return new Panel(table)
        {
            Header = new PanelHeader(
                $"[bold]肌 Service Registrations ({rows.Count})[/]",
                Justify.Left
            ),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Expand = true,
        };
    }

    private Panel CreateRegistrationsAutoHiddenPanel(int count)
    {
        var message =
            $"Registrations list hidden (count: {count} > {RegistrationListAutoHideThreshold}).";
        var instruction =
            "To show, set ConsoleOutput = ConsoleDisplay.Summary | ConsoleDisplay.ListOn | ConsoleDisplay.Issues.";

        var text = new Markup(
            $"[dim]{Markup.Escape(message + Environment.NewLine + instruction)}[/]"
        );
        return new Panel(text)
        {
            Header = new PanelHeader("[bold]肌 Service Registrations (hidden)[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Expand = true,
        };
    }

    private Panel CreateIssuesPanel(
        IReadOnlyList<QudiMissingRegistration> missing,
        IReadOnlyList<QudiCycle> cycles,
        IReadOnlyList<QudiLifetimeWarning> lifetimeWarnings
    )
    {
        var totalIssues = missing.Count + cycles.Count + lifetimeWarnings.Count;

        if (totalIssues == 0)
        {
            var mk = new Markup("[bold green]笨・No issues detected![/]");
            return new Panel(mk)
            {
                Header = new PanelHeader("[bold]孱・・ Issues[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Green),
                Expand = true,
            };
        }

        var grid = new Grid().Expand().Centered().AddColumn();

        if (missing.Count > 0)
        {
            var missingTable = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Color.Red)
                .AddColumn(new TableColumn("[bold red]Required[/]"))
                .AddColumn(new TableColumn("[bold red]Requested by[/]"));

            foreach (var item in missing)
            {
                missingTable.AddRow(
                    $"[red]{Markup.Escape(item.RequiredType)}[/]",
                    $"[red]{Markup.Escape(item.RequestedBy)}[/]"
                );
            }
            grid.AddRow(
                new Panel(missingTable)
                {
                    Header = new PanelHeader($"[bold red]笶・Missing ({missing.Count})[/]"),
                    Border = BoxBorder.None,
                }
            );
        }

        if (cycles.Count > 0)
        {
            var cycleTable = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Color.Magenta1)
                .AddColumn(new TableColumn("[bold magenta]Cycle Path[/]"));

            foreach (var cycle in cycles)
            {
                cycleTable.AddRow($"[magenta]{Markup.Escape(string.Join(" 竊・", cycle.Path))}[/]");
            }
            grid.AddRow(
                new Panel(cycleTable)
                {
                    Header = new PanelHeader($"[bold magenta]売 Cycles ({cycles.Count})[/]"),
                    Border = BoxBorder.None,
                }
            );
        }

        if (lifetimeWarnings.Count > 0)
        {
            var warningTable = new Table()
                .Border(TableBorder.Simple)
                .BorderColor(Color.Orange1)
                .AddColumn(new TableColumn("[bold orange1]Service[/]"))
                .AddColumn(new TableColumn("[bold orange1]From[/]"))
                .AddColumn(new TableColumn("[bold orange1]To[/]"))
                .AddColumn(new TableColumn("[bold orange1]Message[/]"));

            foreach (var item in lifetimeWarnings)
            {
                warningTable.AddRow(
                    $"[orange1]{Markup.Escape(item.Service)}[/]",
                    $"[orange1]{Markup.Escape(item.From)}[/]",
                    $"[orange1]{Markup.Escape(item.To)}[/]",
                    $"[orange1]{Markup.Escape(item.Message)}[/]"
                );
            }
            grid.AddRow(
                new Panel(warningTable)
                {
                    Header = new PanelHeader(
                        $"[bold orange1]庁  Lifetime Warnings ({lifetimeWarnings.Count})[/]"
                    ),
                    Border = BoxBorder.None,
                }
            );
        }

        var borderColor = missing.Count > 0 || cycles.Count > 0 ? Color.Red : Color.Orange1;
        return new Panel(grid)
        {
            Header = new PanelHeader($"[bold]孱・・ Issues ({totalIssues})[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Expand = true,
        };
    }

    private Panel CreateTracesPanel(IReadOnlyList<QudiTraceResult> traces)
    {
        if (traces.Count == 0)
        {
            return new Panel(new Markup("[dim]No traces[/]"))
            {
                Header = new PanelHeader("[bold]剥 Resolution Traces[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Expand = true,
            };
        }

        var grid = new Grid().AddColumn();
        foreach (var trace in traces)
        {
            var root = new Tree($"[bold green]{Markup.Escape(trace.Service)}[/]")
            {
                Style = new Style(Color.Green),
            };

            foreach (var node in trace.Roots)
            {
                AddTraceNode(root, node);
            }
            grid.AddRow(root);
        }

        return new Panel(grid)
        {
            Header = new PanelHeader(
                $"[bold]剥 Resolution Traces ({traces.Count})[/]",
                Justify.Left
            ),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Expand = true,
        };
    }

    private void AddTraceNode(IHasTreeNodes parent, QudiTraceNode node)
    {
        var label = Markup.Escape(node.Label);
        string color;
        var suffix = "";

        if (node.IsMissing)
        {
            color = "red";
            suffix = " ❌";
        }
        else if (node.IsCycle)
        {
            color = "magenta";
            suffix = " 🔄";
        }
        else
        {
            color = "green";
        }

        if (!string.IsNullOrWhiteSpace(node.Detail))
        {
            label += $" [dim]({Markup.Escape(node.Detail!)})[/]";
        }

        var child = parent.AddNode($"[{color}]{label}{suffix}[/]");
        foreach (var nested in node.Children)
        {
            AddTraceNode(child, nested);
        }
    }

    private static bool IsEnabled(ConsoleDisplay display, ConsoleDisplay flag)
    {
        return (display & flag) == flag;
    }

    private static RegistrationListDisplay ResolveRegistrationListDisplay(ConsoleDisplay display)
    {
        if (IsEnabled(display, ConsoleDisplay.ListOn))
        {
            return RegistrationListDisplay.On;
        }

        if (IsEnabled(display, ConsoleDisplay.ListOff))
        {
            return RegistrationListDisplay.Off;
        }

        if (IsEnabled(display, ConsoleDisplay.ListAuto))
        {
            return RegistrationListDisplay.Auto;
        }

        return RegistrationListDisplay.None;
    }

    private enum RegistrationListDisplay
    {
        None,
        Auto,
        On,
        Off,
    }

    private Panel CreateWarningsPanel(IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return new Panel(new Markup("[dim]No warnings[/]"))
            {
                Header = new PanelHeader("[bold]笞・・ Visualizer Warnings[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Expand = true,
            };
        }

        var list = new List<Markup>();
        foreach (var warning in warnings)
        {
            list.Add(new Markup($"[orange1]窶｢ {Markup.Escape(warning)}[/]"));
        }

        var grid = new Grid().AddColumn();
        foreach (var item in list)
        {
            grid.AddRow(item);
        }

        return new Panel(grid)
        {
            Header = new PanelHeader(
                $"[bold]笞・・ Visualizer Warnings ({warnings.Count})[/]",
                Justify.Left
            ),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Orange1),
            Expand = true,
        };
    }
}
