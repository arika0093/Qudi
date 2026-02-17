using System;
using System.Linq;
using System.Threading.Tasks;
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

    [Test]
    public void CompositeWithBoolReturnAggregatesWithAnd()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IValidationService>();

        // Valid input (length > 0 AND all letters)
        validator.Validate("abc").ShouldBeTrue();

        // Invalid: empty string (length check fails)
        validator.Validate("").ShouldBeFalse();

        // Invalid: contains numbers (alpha check fails)
        validator.Validate("abc123").ShouldBeFalse();
    }

    [Test]
    public void CompositeWithUnsupportedReturnThrows()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var dataProvider = provider.GetRequiredService<IDataProvider>();

        Should.Throw<NotSupportedException>(() => dataProvider.GetData().ToList());
    }

    [Test]
    public void CompositeMethodAttributeOverridesResultHandling()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ICompositeMethodService>();

        service.AllCheck().ShouldBeFalse();
        service.AnyCheck().ShouldBeTrue();
    }

    [Test]
    public void CompositeUnsupportedMembersThrow()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ICompositeMethodService>();

        Should.Throw<NotSupportedException>(() => service.UnsupportedMethod());
        Should.Throw<NotSupportedException>(() => _ = service.UnsupportedProperty);
    }

    [Test]
    public async Task CompositeWithTaskReturnAwaitsAll()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var asyncService = provider.GetRequiredService<IAsyncService>();

        CompositeAsyncService.ProcessedItems.Clear();

        await asyncService.ProcessAsync("test");

        CompositeAsyncService.ProcessedItems.Count.ShouldBe(2);
        CompositeAsyncService.ProcessedItems.ShouldContain("A:test");
        CompositeAsyncService.ProcessedItems.ShouldContain("B:test");
    }
}
