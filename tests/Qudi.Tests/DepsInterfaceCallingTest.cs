using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Qudi.Tests.Deps;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class DepsInterfaceCallingTest
{
    private const string TestCondition = nameof(DepsInterfaceCallingTest);

    [Test]
    public void RegistersConcreteAndInterfaceSameInstance()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IDependencyAction>();
        var called = service.SayHello();
        called.ShouldBe("Hello from DependencyAction");
    }

    [Test]
    public void UseSelfImplementsOnlySkipsDependencyRegistrations()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf =>
        {
            conf.SetCondition(TestCondition);
            conf.UseSelfImplementsOnly();
        });

        var provider = services.BuildServiceProvider();
        var dependencyService = provider.GetService<IDependencyAction>();
        var localService = provider.GetRequiredService<ISingletonSample>();

        dependencyService.ShouldBeNull();
        localService.Name.ShouldBe("singleton");
    }

    internal interface ISingletonSample
    {
        string Name { get; }
    }

    [DISingleton(When = [TestCondition])]
    internal sealed class SingletonSample : ISingletonSample
    {
        public string Name => "singleton";
    }
}
