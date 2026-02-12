using System.Collections.Generic;
using Qudi;

namespace Qudi.Tests;

public interface IHelperOnlyService
{
    string Echo(string value);
}

[DITransient]
public sealed class HelperOnlyService : IHelperOnlyService
{
    public string Echo(string value) => value;
}

[DITransient]
public sealed class HelperLogger
{
    public string Prefix => "log";
}

[QudiDecorator]
public sealed partial class HelperOnlyDecorator(
    IHelperOnlyService innerService,
    HelperLogger logger
) : IHelperOnlyService
{
    public string Echo(string value) => $"{logger.Prefix}:{innerService.Echo(value)}";
}

public interface IInterceptService
{
    string Echo(string value);
}

[DITransient]
public sealed class InterceptService : IInterceptService
{
    public string Echo(string value) => value;
}

[DISingleton]
public sealed class InterceptState
{
    public List<string> Entries { get; } = new();

    public void Add(string value) => Entries.Add(value);
}

[QudiDecorator(UseIntercept = true)]
public sealed partial class InterceptDecorator(IInterceptService innerService, InterceptState state)
    : IInterceptService
{
    public IEnumerable<bool> Intercept(string methodName, object?[] args)
    {
        state.Add($"before:{methodName}");
        yield return true;
        state.Add($"after:{methodName}");
    }
}

public interface IAsyncInterceptService
{
    System.Threading.Tasks.Task<string> EchoAsync(string value);
    System.Threading.Tasks.ValueTask<string> EchoValueAsync(string value);
    System.Threading.Tasks.Task DoAsync();
    System.Threading.Tasks.ValueTask DoValueAsync();
}

[DISingleton]
public sealed class AsyncInterceptState
{
    public List<string> Entries { get; } = new();

    public void Add(string value) => Entries.Add(value);
}

[DITransient]
public sealed class AsyncInterceptService(AsyncInterceptState state) : IAsyncInterceptService
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

[QudiDecorator(UseIntercept = true)]
public sealed partial class AsyncInterceptDecorator(
    IAsyncInterceptService innerService,
    AsyncInterceptState state
) : IAsyncInterceptService
{
    public IEnumerable<bool> Intercept(string methodName, object?[] args)
    {
        state.Add($"before:{methodName}");
        yield return true;
        state.Add($"after:{methodName}");
    }
}
