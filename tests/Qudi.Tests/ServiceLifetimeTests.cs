using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class ServiceLifetimeTests
{
    [Test]
    public void RegistersSingletonAsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ISingletonSample>();
        var second = provider.GetRequiredService<ISingletonSample>();

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Test]
    public void RegistersTransientAsDifferentInstances()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITransientSample>();
        var second = provider.GetRequiredService<ITransientSample>();

        ReferenceEquals(first, second).ShouldBeFalse();
    }

    [Test]
    public void RegistersScopedAsSameInstancePerScope()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        var scope1First = scope1.ServiceProvider.GetRequiredService<IScopedSample>();
        var scope1Second = scope1.ServiceProvider.GetRequiredService<IScopedSample>();

        using var scope2 = provider.CreateScope();
        var scope2First = scope2.ServiceProvider.GetRequiredService<IScopedSample>();

        ReferenceEquals(scope1First, scope1Second).ShouldBeTrue();
        ReferenceEquals(scope1First, scope2First).ShouldBeFalse();
    }
}
