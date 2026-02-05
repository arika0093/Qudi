using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Qudi.Tests.Deps;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class DepsInterfaceCallingTest
{
    [Test]
    public void RegistersConcreteAndInterfaceSameInstance()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IDependencyAction>();
        var called = service.SayHello();
        called.ShouldBe("Hello from DependencyAction");
    }

    [Test]
    public void UseSelfImplementsOnlySkipsDependencyRegistrations()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.UseSelfImplementsOnly());

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IDependencyAction>();

        service.ShouldBeNull();
    }
}
