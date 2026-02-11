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
public sealed partial class HelperDecorator : IHelperService
{
    public partial HelperDecorator(IHelperService inner);

    public override string Echo(string value) => $"decorator({base.Echo(value)})";
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
public sealed partial class OrderedDecorator : IOrderedService
{
    public partial OrderedDecorator(IOrderedService innerService);

    public override string Get() => $"decorator({base.Get()})";

    private void CheckVariableIsExist()
    {
        var _ = innerService == null;
    }
}
