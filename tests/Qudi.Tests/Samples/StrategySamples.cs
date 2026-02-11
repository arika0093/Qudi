using Qudi;

namespace Qudi.Tests;

public interface IHelperService
{
    string Echo(string value);
}

[DITransient]
public sealed class HelperService : IHelperService
{
    public string Echo(string value) => value;
}

[QudiDecorator]
public sealed partial class HelperDecorator(IHelperService inner) : IHelperService
{
    public string Echo(string value) => $"decorator({Base.Echo(value)})";
}

public interface IOrderedService
{
    string Get();
}

[DITransient]
public sealed class OrderedService : IOrderedService
{
    public string Get() => "base";
}

[QudiDecorator]
public sealed partial class OrderedDecorator(IOrderedService innerService) : IOrderedService
{
    public string Get() => $"decorator({Base.Get()})";

    private void CheckVariableIsExist()
    {
        var _ = innerService == null;
    }
}
