using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class DecoratorInterceptAsyncTests
{
    private const string TestCondition = nameof(DecoratorInterceptAsyncTests);

    [Test]
    public async System.Threading.Tasks.Task InterceptsTaskResult()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IAsyncInterceptService>();
        var state = provider.GetRequiredService<AsyncInterceptState>();

        var result = await service.EchoAsync("hello");
        result.ShouldBe("hello");
        AssertLastSequence(state, "EchoAsync");
    }

    [Test]
    public async System.Threading.Tasks.Task InterceptsValueTaskResult()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IAsyncInterceptService>();
        var state = provider.GetRequiredService<AsyncInterceptState>();

        var valueResult = await service.EchoValueAsync("hi");
        valueResult.ShouldBe("hi");
        AssertLastSequence(state, "EchoValueAsync");
    }

    [Test]
    public async System.Threading.Tasks.Task InterceptsTaskVoid()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IAsyncInterceptService>();
        var state = provider.GetRequiredService<AsyncInterceptState>();

        await service.DoAsync();
        AssertLastSequence(state, "DoAsync");
    }

    [Test]
    public async System.Threading.Tasks.Task InterceptsValueTaskVoid()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IAsyncInterceptService>();
        var state = provider.GetRequiredService<AsyncInterceptState>();

        await service.DoValueAsync();
        AssertLastSequence(state, "DoValueAsync");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));
        return services.BuildServiceProvider();
    }

    private static void AssertLastSequence(AsyncInterceptState state, string methodName)
    {
        var entries = state.Entries;
        entries.Count.ShouldBeGreaterThanOrEqualTo(4);

        var lastEntries = entries.Skip(entries.Count - 4).ToArray();
        lastEntries.ShouldBe([
            $"before:{methodName}",
            $"service:start:{methodName}",
            $"service:end:{methodName}",
            $"after:{methodName}",
        ]);
    }

    public interface IAsyncInterceptService
    {
        System.Threading.Tasks.Task<string> EchoAsync(string value);
        System.Threading.Tasks.ValueTask<string> EchoValueAsync(string value);
        System.Threading.Tasks.Task DoAsync();
        System.Threading.Tasks.ValueTask DoValueAsync();
    }

    [DISingleton(When = [TestCondition])]
    internal sealed class AsyncInterceptState
    {
        public System.Collections.Generic.List<string> Entries { get; } = new();

        public void Add(string value) => Entries.Add(value);
    }

    [DITransient(When = [TestCondition])]
    internal sealed class AsyncInterceptService(AsyncInterceptState state) : IAsyncInterceptService
    {
        public async System.Threading.Tasks.Task<string> EchoAsync(string value)
        {
            state.Add("service:start:EchoAsync");
            await System.Threading.Tasks.Task.Delay(1).ConfigureAwait(false);
            state.Add("service:end:EchoAsync");
            return value;
        }

        public async System.Threading.Tasks.ValueTask<string> EchoValueAsync(string value)
        {
            state.Add("service:start:EchoValueAsync");
            await System.Threading.Tasks.Task.Delay(1).ConfigureAwait(false);
            state.Add("service:end:EchoValueAsync");
            return value;
        }

        public async System.Threading.Tasks.Task DoAsync()
        {
            state.Add("service:start:DoAsync");
            await System.Threading.Tasks.Task.Delay(1).ConfigureAwait(false);
            state.Add("service:end:DoAsync");
        }

        public async System.Threading.Tasks.ValueTask DoValueAsync()
        {
            state.Add("service:start:DoValueAsync");
            await System.Threading.Tasks.Task.Delay(1).ConfigureAwait(false);
            state.Add("service:end:DoValueAsync");
        }
    }

    [QudiDecorator(UseIntercept = true, When = [TestCondition])]
    internal sealed partial class AsyncInterceptDecorator(
        IAsyncInterceptService innerService,
        AsyncInterceptState state
    ) : IAsyncInterceptService
    {
        public System.Collections.Generic.IEnumerable<bool> Intercept(
            string methodName,
            object?[] args
        )
        {
            state.Add($"before:{methodName}");
            yield return true;
            state.Add($"after:{methodName}");
        }
    }
}
