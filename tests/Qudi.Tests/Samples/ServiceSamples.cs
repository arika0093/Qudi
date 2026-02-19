using System;
using System.Collections.Generic;
using System.Linq;
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
public class MessageService : IMessageService
{
    public string Send(string message) => message;
}

[QudiDecorator(Order = 1)]
public partial class DecoratorOne(IMessageService foo) : IMessageService
{
    public string Send(string message) => $"D1({foo.Send(message)})";
}

[QudiDecorator(Order = 2)]
public partial class DecoratorTwo(IMessageService inner) : IMessageService
{
    public string Send(string message) => $"D2({inner.Send(message)})";
}

public interface ISend
{
    string Msg(string a);
}

[DISingleton]
public sealed class SendA : ISend
{
    public string Msg(string a) => $"A:{a}";
}

[DISingleton]
public sealed class SendB : ISend
{
    public string Msg(string a) => $"B:{a}";
}

[QudiDecorator]
public sealed partial class SendDecorator(ISend send) : ISend
{
    public string Msg(string a) => send.Msg($"MESSAGE IS {a}");
}

[DISingleton]
public sealed class SendAll(IEnumerable<ISend> sends)
{
    public string[] SendAllMessages(string message)
    {
        return sends.Select(s => s.Msg(message)).ToArray();
    }
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

public interface IKeyedLiteralSample
{
    string Value { get; }
}

[DITransient(Key = "a\\\"b\\\\c")]
public sealed class KeyedLiteralStringSample : IKeyedLiteralSample
{
    public string Value => "quoted";
}

[DITransient(Key = 42)]
public sealed class KeyedLiteralIntSample : IKeyedLiteralSample
{
    public string Value => "forty-two";
}

[DITransient(Key = "concrete-only")]
public sealed class KeyedConcreteOnlySample
{
    public string Value => "concrete";
}

public interface IKeyedDecoratedSample
{
    string Value { get; }
}

[DITransient(Key = "decorated")]
public sealed class KeyedDecoratedSample : IKeyedDecoratedSample
{
    public string Value => "base";
}

[QudiDecorator]
public sealed partial class KeyedDecoratedSampleDecorator(IKeyedDecoratedSample inner)
    : IKeyedDecoratedSample
{
    public string Value => $"decorated:{inner.Value}";
}

public interface ITransientDecoratedSample
{
    string Id { get; }
}

[DITransient]
public sealed class TransientDecoratedSample : ITransientDecoratedSample
{
    public string Id { get; } = Guid.NewGuid().ToString();
}

[QudiDecorator]
public sealed partial class TransientDecoratedSampleDecorator(ITransientDecoratedSample inner)
    : ITransientDecoratedSample
{
    public string Id { get; } = inner.Id;
}

public interface IQudiAttributeSample
{
    string Value { get; }
}

[Qudi(Lifetime = Lifetime.Singleton, AsTypes = [typeof(IQudiAttributeSample)])]
public sealed class QudiAttributeSample : IQudiAttributeSample
{
    public string Value => "by-qudi-attribute";
}

public interface IAsTypesSelfOnlySample
{
    string Id { get; }
}

[Qudi(AsTypesFallback = AsTypesFallback.Self)]
public sealed class AsTypesSelfOnlySample : IAsTypesSelfOnlySample
{
    public string Id => "self-only";
}

public interface IAsTypesInterfacesOnlySample
{
    string Id { get; }
}

[Qudi(AsTypesFallback = AsTypesFallback.Interfaces)]
public sealed class AsTypesInterfacesOnlySample : IAsTypesInterfacesOnlySample
{
    public string Id => "interfaces-only";
}

public interface IAsTypesSelfWithInterfacesSample
{
    string Id { get; }
}

[Qudi(AsTypesFallback = AsTypesFallback.SelfWithInterfaces)]
public sealed class AsTypesSelfWithInterfacesSample : IAsTypesSelfWithInterfacesSample
{
    public string Id => "self-with-interface";
}

public interface IAsTypesSelfOrInterfacesSample
{
    string Id { get; }
}

[Qudi(AsTypesFallback = AsTypesFallback.SelfOrInterfaces)]
public sealed class AsTypesSelfOrInterfacesSample : IAsTypesSelfOrInterfacesSample
{
    public string Id => "self-or-interfaces";
}

public interface IAsTypesDefaultSample
{
    string Id { get; }
}

[Qudi]
public sealed class AsTypesDefaultSample : IAsTypesDefaultSample
{
    public string Id => "default-interface";
}

[Qudi]
public sealed class AsTypesDefaultSelfSample
{
    public string Id => "default-self";
}

public interface IDuplicateAddSample
{
    string Id { get; }
}

[Qudi(Duplicate = DuplicateHandling.Add, Order = 0)]
public sealed class DuplicateAddSampleFirst : IDuplicateAddSample
{
    public string Id => "add-first";
}

[Qudi(Duplicate = DuplicateHandling.Add, Order = 1)]
public sealed class DuplicateAddSampleSecond : IDuplicateAddSample
{
    public string Id => "add-second";
}

public interface IDuplicateSkipSample
{
    string Id { get; }
}

[Qudi(Duplicate = DuplicateHandling.Skip, Order = 0)]
public sealed class DuplicateSkipSampleFirst : IDuplicateSkipSample
{
    public string Id => "skip-first";
}

[Qudi(Duplicate = DuplicateHandling.Skip, Order = 1)]
public sealed class DuplicateSkipSampleSecond : IDuplicateSkipSample
{
    public string Id => "skip-second";
}

public interface IDuplicateReplaceSample
{
    string Id { get; }
}

[Qudi(Duplicate = DuplicateHandling.Replace, Order = 0)]
public sealed class DuplicateReplaceSampleFirst : IDuplicateReplaceSample
{
    public string Id => "replace-first";
}

[Qudi(Duplicate = DuplicateHandling.Replace, Order = 1)]
public sealed class DuplicateReplaceSampleSecond : IDuplicateReplaceSample
{
    public string Id => "replace-second";
}

public interface IDuplicateThrowSample
{
    string Id { get; }
}

[Qudi(Duplicate = DuplicateHandling.Throw, Order = 0, When = ["ThrowTest"])]
public sealed class DuplicateThrowSampleFirst : IDuplicateThrowSample
{
    public string Id => "throw-first";
}

[Qudi(Duplicate = DuplicateHandling.Throw, Order = 1, When = ["ThrowTest"])]
public sealed class DuplicateThrowSampleSecond : IDuplicateThrowSample
{
    public string Id => "throw-second";
}
