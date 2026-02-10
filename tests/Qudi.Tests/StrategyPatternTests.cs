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

    [Test]
    public void DecoratorHelperUsesExtraArguments()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IHelperOnlyService>();

        service.Echo("hi").ShouldBe("log:hi");
    }

    [Test]
    public void DecoratorHelperInterceptRecordsCalls()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IInterceptService>();
        var state = provider.GetRequiredService<InterceptState>();

        service.Echo("hi").ShouldBe("hi");
        state.Entries.ShouldBe(["before:Echo", "after:Echo"]);
    }

    [Test]
    public void StrategyHelperUsesExtraArguments()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ILoggedStrategyService>();

        service.Get().ShouldBe("log:beta");
    }
}
