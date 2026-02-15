using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qudi;
using Qudi.Examples;
using Qudi.Visualizer;
using Spectre.Console;

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// ✅️ register services marked with Qudi attributes (see below)
services.AddQudiServices(conf =>
{
    conf.EnableVisualizationOutput(option =>
    {
        option.ConsoleOutput = ConsoleDisplay.All;
        option.LoggerOutput = LoggerOutput.All;
        option.LoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        option.SetOutputDirectory(
            "exported/",
            [QudiVisualizationFormat.Markdown, QudiVisualizationFormat.Dot]
        );
    });
});

var provider = services.BuildServiceProvider();

// Get all sample executors
var executors = provider.GetServices<ISampleExecutor>().ToList();

if (!executors.Any())
{
    AnsiConsole.MarkupLine("[red]No sample executors found![/]");
    return;
}

// Display header
AnsiConsole.Write(new FigletText("Qudi Examples").Centered().Color(Color.Blue));

// Create selection prompt
try
{
    var selection = AnsiConsole.Prompt(
        new SelectionPrompt<ISampleExecutor>()
            .Title("[green]Select a sample to run:[/]")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more samples)[/]")
            .AddChoices(executors)
            .UseConverter(executor => Markup.Escape($"{executor.Name} - {executor.Description}"))
    );
    SampleExecute(selection);
}
catch (NotSupportedException)
{
    // In non-interactive environments (e.g., during testing),
    // the selection prompt is not available, so we execute all samples sequentially.
    foreach (var executor in executors)
    {
        SampleExecute(executor);
    }
}

// Display footer
AnsiConsole.WriteLine();
AnsiConsole.Write(new Rule("[grey]End of sample[/]").RuleStyle("grey").LeftJustified());

void SampleExecute(ISampleExecutor selection)
{
    // Display separator
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[yellow]{selection.Name}[/]").RuleStyle("grey").LeftJustified());
    AnsiConsole.WriteLine();

    // Execute the selected sample
    try
    {
        selection.Execute();
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error executing sample: {ex.Message}[/]");
        AnsiConsole.WriteException(ex);
    }
}
