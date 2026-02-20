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
    public void CompositeWithEnumerableReturnCombinesResults()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var dataProvider = provider.GetRequiredService<IDataProvider>();

        var data = dataProvider.GetData().ToList();
        data.ShouldNotBeNull();
        data.Count.ShouldBe(4); // A1, A2, B1, B2
        data.ShouldContain("A1");
        data.ShouldContain("A2");
        data.ShouldContain("B1");
        data.ShouldContain("B2");
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

    [Test]
    public async Task CompositeWithSequentialTaskExecutesInOrder()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var sequentialService = provider.GetRequiredService<ISequentialAsyncService>();

        CompositeSequentialAsyncService.ExecutionOrder.Clear();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await sequentialService.ExecuteAsync("test");
        stopwatch.Stop();

        // Sequential execution should take at least 100ms (50ms * 2)
        // whereas parallel would take only 50ms
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan(90);

        CompositeSequentialAsyncService.ExecutionOrder.Count.ShouldBe(2);

        // Parse timestamps and verify sequential order
        var aTicks = long.Parse(CompositeSequentialAsyncService.ExecutionOrder[0].Split(':')[2]);
        var bTicks = long.Parse(CompositeSequentialAsyncService.ExecutionOrder[1].Split(':')[2]);

        // B should execute after A (timestamp should be later)
        bTicks.ShouldBeGreaterThan(aTicks);
    }

    [Test]
    public void CompositeWithCustomAggregatorCombinesFlags()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var flagService = provider.GetRequiredService<IFlagService>();

        var flags = flagService.GetFlags();
        flags.ShouldBe(AccessFlags.Read | AccessFlags.Write);
    }
}
