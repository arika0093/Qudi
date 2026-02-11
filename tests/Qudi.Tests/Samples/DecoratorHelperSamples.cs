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
public sealed partial class HelperOnlyDecorator : IHelperOnlyService
{
    public partial HelperOnlyDecorator(IHelperOnlyService innerService, HelperLogger logger);

    public override string Echo(string value) => $"{logger.Prefix}:{base.Echo(value)}";
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

[QudiDecorator]
public sealed partial class InterceptDecorator : IInterceptService
{
    public partial InterceptDecorator(IInterceptService innerService, InterceptState state);

    protected override IEnumerable<bool> Intercept(string methodName, object?[] args)
    {
        state.Add($"before:{methodName}");
        yield return true;
        state.Add($"after:{methodName}");
    }
}
