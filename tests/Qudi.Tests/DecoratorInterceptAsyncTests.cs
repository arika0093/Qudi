using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class DecoratorInterceptAsyncTests
{
    [Test]
    public async System.Threading.Tasks.Task InterceptAwaitsTaskLikeMethods()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IAsyncInterceptService>();
        var state = provider.GetRequiredService<AsyncInterceptState>();

        var result = await service.EchoAsync("hello");
        result.ShouldBe("hello");
        AssertLastSequence(state, "EchoAsync");

        var valueResult = await service.EchoValueAsync("hi");
        valueResult.ShouldBe("hi");
        AssertLastSequence(state, "EchoValueAsync");

        await service.DoAsync();
        AssertLastSequence(state, "DoAsync");

        await service.DoValueAsync();
        AssertLastSequence(state, "DoValueAsync");
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
}
