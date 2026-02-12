using Qudi;
using Qudi.Example.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddQudiServices(conf =>
{
    conf.EnableVisualizationOutput(option =>
    {
        option.AddOutput("assets/visualization_output.json");
        option.AddOutput("assets/visualization_output.dot");
        option.AddOutput("assets/visualization_output.mermaid");
        option.AddOutput("assets/visualization_output.dgml");
        option.EnableConsoleOutput = true;
    });
    conf.SetCondition(builder.Environment.EnvironmentName);
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
