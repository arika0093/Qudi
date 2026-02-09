using System.Collections.Generic;
using Qudi;
using Qudi.Helper;
using Qudi.Helper.Qudi_Tests_IHelperService;
using Qudi.Helper.Qudi_Tests_IOrderedService;
using Qudi.Helper.Qudi_Tests_IStrategyService;

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

[QudiDecorator(Lifetime = Lifetime.Transient, Order = 0, AsTypes = [typeof(IHelperService)])]
public sealed class HelperDecorator(IHelperService inner)
    : Qudi.Helper.Qudi_Tests_IHelperService.DecoratorHelper<IHelperService>(inner)
{
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

[QudiStrategy(Lifetime = Lifetime.Singleton, Order = 0, AsTypes = [typeof(IStrategyService)])]
public sealed class StrategySelector(IEnumerable<IStrategyService> services)
    : Qudi.Helper.Qudi_Tests_IStrategyService.StrategyHelper<IStrategyService>(services)
{
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

[QudiDecorator(Lifetime = Lifetime.Transient, Order = 0, AsTypes = [typeof(IOrderedService)])]
public sealed class OrderedDecorator(IOrderedService inner)
    : Qudi.Helper.Qudi_Tests_IOrderedService.DecoratorHelper<IOrderedService>(inner)
{
    public override string Get() => $"decorator({base.Get()})";
}

[QudiStrategy(Lifetime = Lifetime.Singleton, Order = 0, AsTypes = [typeof(IOrderedService)])]
public sealed class OrderedStrategy(IEnumerable<IOrderedService> services)
    : Qudi.Helper.Qudi_Tests_IOrderedService.StrategyHelper<IOrderedService>(services)
{
    protected override StrategyResult ShouldUseService(IOrderedService service)
    {
        return new StrategyResult { UseService = true, Continue = false };
    }

    public override string Get() => $"strategy({base.Get()})";
}
