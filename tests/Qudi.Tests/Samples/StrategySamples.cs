using System.Linq;
using System.Collections.Generic;
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

[QudiDecorator(Lifetime = Lifetime.Transient)]
public sealed partial class HelperDecorator : IHelperService
{
    public partial HelperDecorator(IHelperService inner);

    public override string Echo(string value) => $"decorator({base.Echo(value)})";
}

public interface IStrategyService
{
    string Name { get; }
}

[DITransient]
public sealed class StrategyServiceAlpha : IStrategyService
{
    public string Name => "alpha";
}

[DITransient]
public sealed class StrategyServiceBeta : IStrategyService
{
    public string Name => "beta";
}

[QudiStrategy(Lifetime = Lifetime.Singleton)]
public sealed partial class StrategySelector : IStrategyService
{
    public partial StrategySelector(IEnumerable<IStrategyService> services);

    protected override StrategyResult ShouldUseService(IStrategyService service)
    {
        return new StrategyResult
        {
            UseService = service is StrategyServiceBeta,
            Continue = service is not StrategyServiceBeta,
        };
    }
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

[QudiDecorator(Lifetime = Lifetime.Transient)]
public sealed partial class OrderedDecorator : IOrderedService
{
    public partial OrderedDecorator(IOrderedService innerService);

    public override string Get() => $"decorator({base.Get()})";

    private void CheckVariableIsExist()
    {
        var _ = innerService == null;
    }
}

[QudiStrategy(Lifetime = Lifetime.Singleton)]
public sealed partial class OrderedStrategy : IOrderedService
{
    public partial OrderedStrategy(IEnumerable<IOrderedService> myServices);

    protected override StrategyResult ShouldUseService(IOrderedService service) => true;

    public override string Get() => $"strategy({base.Get()})";

    private void CheckVariableIsExist()
    {
        var _ = myServices.Any();
    }
}
