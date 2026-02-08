using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class DecoratorKeyedAndLifetimeTests
{
    [Test]
    public void AppliesDecoratorsToKeyedRegistrations()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var keyed = provider.GetRequiredKeyedService<IKeyedDecoratedSample>("decorated");
        var unkeyed = provider.GetService<IKeyedDecoratedSample>();

        keyed.Value.ShouldBe("decorated:base");
        unkeyed.ShouldBeNull();
    }

    [Test]
    public void DecoratorsInheritLifetimeFromInnerRegistration()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITransientDecoratedSample>();
        var second = provider.GetRequiredService<ITransientDecoratedSample>();

        first.Id.ShouldNotBe(second.Id);
    }
}
