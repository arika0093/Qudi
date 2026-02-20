using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class KeyedLiteralAndConcreteTests
{
    private const string TestCondition = nameof(KeyedLiteralAndConcreteTests);

    [Test]
    public void RegistersKeyedLiteralValues()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var quoted = provider.GetRequiredKeyedService<IKeyedLiteralSample>("a\\\"b\\\\c");
        var numeric = provider.GetRequiredKeyedService<IKeyedLiteralSample>(42);

        quoted.Value.ShouldBe("quoted");
        numeric.Value.ShouldBe("forty-two");
    }

    [Test]
    public void RegistersKeyedConcreteOnlyAsKeyedService()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var concrete = provider.GetService<KeyedConcreteOnlySample>();
        var keyed = provider.GetKeyedService<KeyedConcreteOnlySample>("concrete-only");

        concrete.ShouldBeNull();
        keyed.ShouldNotBeNull();
    }

    internal interface IKeyedLiteralSample
    {
        string Value { get; }
    }

    [DITransient(Key = "a\\\"b\\\\c", When = [TestCondition])]
    internal sealed class KeyedLiteralStringSample : IKeyedLiteralSample
    {
        public string Value => "quoted";
    }

    [DITransient(Key = 42, When = [TestCondition])]
    internal sealed class KeyedLiteralIntSample : IKeyedLiteralSample
    {
        public string Value => "forty-two";
    }

    [DITransient(Key = "concrete-only", When = [TestCondition])]
    internal sealed class KeyedConcreteOnlySample
    {
        public string Value => "concrete";
    }
}
