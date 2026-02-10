using System;
using System.Collections.Generic;
using System.Linq;
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

[QudiStrategy]
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

[QudiStrategy]
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

public interface ILifetimeStrategyService
{
    Guid Id { get; }
}

[DISingleton]
public sealed class LifetimeStrategySingleton : ILifetimeStrategyService
{
    public Guid Id { get; } = Guid.NewGuid();
}

[QudiStrategy]
public sealed partial class LifetimeStrategySelector : ILifetimeStrategyService
{
    public partial LifetimeStrategySelector(IEnumerable<ILifetimeStrategyService> services);

    protected override StrategyResult ShouldUseService(ILifetimeStrategyService service)
    {
        return service is LifetimeStrategySingleton;
    }
}

public interface IScopedStrategyService
{
    Guid Id { get; }
}

[DIScoped]
public sealed class ScopedStrategyService : IScopedStrategyService
{
    public Guid Id { get; } = Guid.NewGuid();
}

[QudiStrategy]
public sealed partial class ScopedStrategySelector : IScopedStrategyService
{
    public partial ScopedStrategySelector(IEnumerable<IScopedStrategyService> services);

    protected override StrategyResult ShouldUseService(IScopedStrategyService service)
    {
        return true;
    }
}
