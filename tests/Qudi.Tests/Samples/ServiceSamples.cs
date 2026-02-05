using Qudi;

namespace Qudi.Tests;

public interface ISingletonSample
{
    string Name { get; }
}

[DISingleton]
public sealed class SingletonSample : ISingletonSample
{
    public string Name => "singleton";
}

public interface ITransientSample
{
    string Name { get; }
}

[DITransient]
public sealed class TransientSample : ITransientSample
{
    public string Name => "transient";
}

public interface IScopedSample
{
    string Name { get; }
}

[DIScoped]
public sealed class ScopedSample : IScopedSample
{
    public string Name => "scoped";
}

public interface IConditionSample
{
    string Marker { get; }
}

[DITransient(When = ["Testing"])]
public sealed class ConditionSampleTesting : IConditionSample
{
    public string Marker => "testing";
}

[DITransient(When = ["Production"])]
public sealed class ConditionSampleProduction : IConditionSample
{
    public string Marker => "production";
}

public interface IMessageService
{
    string Send(string message);
}

[DITransient]
public sealed class MessageService : IMessageService
{
    public string Send(string message) => message;
}

[QudiDecorator(Lifetime = Lifetime.Transient, Order = 1)]
public sealed class DecoratorOne(IMessageService inner) : IMessageService
{
    public string Send(string message) => $"D1({inner.Send(message)})";
}

[QudiDecorator(Lifetime = Lifetime.Transient, Order = 2)]
public sealed class DecoratorTwo(IMessageService inner) : IMessageService
{
    public string Send(string message) => $"D2({inner.Send(message)})";
}

public interface IKeyedSample
{
    string Value { get; }
}

[DITransient(Key = "alpha")]
public sealed class KeyedSample : IKeyedSample
{
    public string Value => "alpha";
}
