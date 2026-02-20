using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class KeyedRegistrationTests
{
    private const string TestCondition = nameof(KeyedRegistrationTests);

    [Test]
    public void RegistersKeyedServices()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IKeyedSample>("alpha");

        service.Value.ShouldBe("alpha");
        provider.GetService<IKeyedSample>().ShouldBeNull();
    }

    internal interface IKeyedSample
    {
        string Value { get; }
    }

    [DITransient(Key = "alpha", When = [TestCondition])]
    internal sealed class KeyedSample : IKeyedSample
    {
        public string Value => "alpha";
    }
}
