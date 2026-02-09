using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class StrategyPatternTests
{
    [Test]
    public void DecoratorHelperForwardsCalls()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IHelperService>();

        service.Echo("hi").ShouldBe("decorator(hi)");
    }

    [Test]
    public void StrategyHelperSelectsExpectedService()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IStrategyService>();

        service.Name.ShouldBe("beta");
    }

    [Test]
    public void AppliesDecoratorBeforeStrategyForSameOrder()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IOrderedService>();

        service.Get().ShouldBe("strategy(decorator(base))");
    }
}
