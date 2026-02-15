using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;

namespace Qudi.Visualizer;

internal class QudiVisualizationConsoleRenderer(IAnsiConsole AnsiConsole)
{
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
        var rule = new Rule("[bold cyan]🎯 Qudi Dependency Injection Visualization[/]")
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
        if (IsEnabled(display, ConsoleDisplay.List))
        {
            AnsiConsole.Write(CreateRegistrationsPanel(report.Registrations));
            AnsiConsole.WriteLine();
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

        if (warnings.Count > 0)
        {
            AnsiConsole.Write(CreateWarningsPanel(warnings));
            AnsiConsole.WriteLine();
        }

        var errorCount = report.MissingRegistrations.Count + report.Cycles.Count;
        var warningCount = report.LifetimeWarnings.Count + warnings.Count;
        AskContinue(errorCount, warningCount);
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
            new Markup("[bold green]📊 Registrations[/]"),
            new Markup("[bold red]❌ Missing[/]"),
            new Markup("[bold magenta]🔄 Cycles[/]"),
            new Markup("[bold orange1]💡 Lifetime[/]")
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
            Header = new PanelHeader("[bold]📈 Summary Metrics[/]", Justify.Left),
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
                "Singleton" => "🔒",
                "Scoped" => "📦",
                "Transient" => "⚡",
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
                $"[bold]🔧 Service Registrations ({rows.Count})[/]",
                Justify.Left
            ),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
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
            var mk = new Markup("[bold green]✅ No issues detected![/]");
            return new Panel(mk)
            {
                Header = new PanelHeader("[bold]🛡️  Issues[/]", Justify.Left),
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
                    Header = new PanelHeader($"[bold red]❌ Missing ({missing.Count})[/]"),
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
                cycleTable.AddRow($"[magenta]{Markup.Escape(string.Join(" → ", cycle.Path))}[/]");
            }
            grid.AddRow(
                new Panel(cycleTable)
                {
                    Header = new PanelHeader($"[bold magenta]🔄 Cycles ({cycles.Count})[/]"),
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
                    Header = new PanelHeader("[bold]🔍 Resolution Traces[/]", Justify.Left),
                    Border = BoxBorder.None,
                }
            );
        }

        var borderColor = missing.Count > 0 || cycles.Count > 0 ? Color.Red : Color.Orange1;
        return new Panel(grid)
        {
            Header = new PanelHeader($"[bold]🛡️  Issues ({totalIssues})[/]", Justify.Left),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Expand = true,
        };
    }

    private static bool IsEnabled(ConsoleDisplay display, ConsoleDisplay flag)
    {
        return (display & flag) == flag;
    }

    private Panel CreateWarningsPanel(IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return new Panel(new Markup("[dim]No warnings[/]"))
            {
                Header = new PanelHeader("[bold]💡  Visualizer Warnings[/]", Justify.Left),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Expand = true,
            };
        }

        var list = new List<Markup>();
        foreach (var warning in warnings)
        {
            list.Add(new Markup($"[orange1]• {Markup.Escape(warning)}[/]"));
        }

        var grid = new Grid().AddColumn();
        foreach (var item in list)
        {
            grid.AddRow(item);
        }

        return new Panel(grid)
        {
            Header = new PanelHeader(
                $"[bold]💡  Visualizer Warnings ({warnings.Count})[/]",
                Justify.Left
            ),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Orange1),
            Expand = true,
        };
    }

    private bool AskContinue(int errorCount, int warningCount)
    {
        try
        {
            if (errorCount > 0)
            {
                // ask continue only if there are issues, otherwise just show the summary
                var isContinue = AnsiConsole.Confirm(
                    $"[bold red]There are {errorCount} errors detected. Do you want to continue?[/]"
                );
                if (!isContinue)
                {
                    Environment.Exit(1);
                }
            }
            if (warningCount > 0)
            {
                // ask too if there are warnings, but less urgent than errors
                var isContinue = AnsiConsole.Confirm(
                    $"[bold orange1]There are {warningCount} warnings detected. Do you want to continue?[/]"
                );
                if (!isContinue)
                {
                    Environment.Exit(1);
                }
            }
            return true;
        }
        catch (NotSupportedException)
        {
            // ignore if console input is not supported (e.g. in some IDEs or environments)
            return true;
        }
    }
}
