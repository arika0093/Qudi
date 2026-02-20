using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class QudiAttributeRegistrationTests
{
    private const string TestCondition = nameof(QudiAttributeRegistrationTests);

    [Test]
    public void RegistersServicesMarkedWithQudiAttribute()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IQudiAttributeSample>();
        var second = provider.GetRequiredService<IQudiAttributeSample>();

        first.Value.ShouldBe("by-qudi-attribute");
        ReferenceEquals(first, second).ShouldBeTrue();
    }

    internal interface IQudiAttributeSample
    {
        string Value { get; }
    }

    [Qudi(Lifetime = Lifetime.Singleton, AsTypes = [typeof(IQudiAttributeSample)], When = [TestCondition])]
    internal sealed class QudiAttributeSample : IQudiAttributeSample
    {
        public string Value => "by-qudi-attribute";
    }
}
