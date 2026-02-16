using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class CompositeTests
{
    [Test]
    public void CompositeCallsAllServices()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var composite = provider.GetRequiredService<INotificationService>();

        var result = CompositeNotificationService.Messages;
        result.Clear();

        composite.Notify("test message");

        result.Count.ShouldBe(2);
        result.ShouldContain("Email: test message");
        result.ShouldContain("SMS: test message");
    }

    [Test]
    public void CompositeWithPartialClass()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMessageService2>();

        var result = CompositeMessageService.Messages;
        result.Clear();

        service.Send("test");

        result.Count.ShouldBe(2);
        result.ShouldContain("ServiceA: test");
        result.ShouldContain("ServiceB: test");
    }
}
