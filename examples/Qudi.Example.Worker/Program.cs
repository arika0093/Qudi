using Qudi;
using Qudi.Example.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddQudiServices(conf =>
{
    conf.SetCondition(builder.Environment.EnvironmentName);
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
