using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class KeyedLiteralAndConcreteTests
{
    [Test]
    public void RegistersKeyedLiteralValues()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var quoted = provider.GetRequiredKeyedService<IKeyedLiteralSample>("a\\\"b\\\\c");
        var numeric = provider.GetRequiredKeyedService<IKeyedLiteralSample>(42);

        quoted.Value.ShouldBe("quoted");
        numeric.Value.ShouldBe("forty-two");
    }

    [Test]
    public void RegistersConcreteOnlyWhenNoInterfaces()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var concrete = provider.GetService<KeyedConcreteOnlySample>();
        var keyed = provider.GetKeyedService<KeyedConcreteOnlySample>("concrete-only");

        concrete.ShouldNotBeNull();
        keyed.ShouldBeNull();
    }
}
