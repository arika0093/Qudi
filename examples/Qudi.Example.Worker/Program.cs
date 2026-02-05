using Qudi;
using Qudi.Example.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddQudiServices(conf =>
{
    conf.SetConditionFromHostEnvironment(builder.Environment);
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
