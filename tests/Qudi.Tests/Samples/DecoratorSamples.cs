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
public sealed class HelperDecorator(IHelperService inner) : IHelperService
{
    public string Echo(string value) => $"decorator({inner.Echo(value)})";
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
public sealed class OrderedDecorator(IOrderedService innerService) : IOrderedService
{
    public string Get() => $"decorator({innerService.Get()})";
}

public partial class PartialTest
{
    // it is checked that the partial class is generated correctly
    [QudiDecorator]
    public partial class PartialDecorator(IHelperService inner) : IHelperService;
}
