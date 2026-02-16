using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class DecoratorTests
{
    [Test]
    public void AppliesDecoratorsInOrder()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMessageService>();

        service.Send("hello").ShouldBe("D2(D1(hello))");
    }

    [Test]
    public void AppliesDecoratorToAllImplementationsInEnumeration()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<SendAll>();

        var results = sender.SendAllMessages("Test");

        results.Length.ShouldBe(2);
        results.ShouldContain("A:MESSAGE IS Test");
        results.ShouldContain("B:MESSAGE IS Test");
    }
}
