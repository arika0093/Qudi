using Qudi;
using Qudi.Example.Worker;
using Qudi.Visualizer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddQudiServices(conf =>
{
    conf.EnableVisualizationOutput(option =>
    {
        option.ConsoleOutput = ConsoleDisplay.All;
        option.AddOutput("assets/visualization_output.md");
        option.SetOutputDirectory("assets/exported", QudiVisualizationFormat.Mermaid);
        option.EnableConsoleOutput = true;
    });
    conf.SetCondition(builder.Environment.EnvironmentName);
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
