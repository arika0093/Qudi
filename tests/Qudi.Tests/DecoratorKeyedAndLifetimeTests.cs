using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed partial class DecoratorKeyedAndLifetimeTests
{
    private const string TestCondition = nameof(DecoratorKeyedAndLifetimeTests);

    [Test]
    public void AppliesDecoratorsToKeyedRegistrations()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var keyed = provider.GetRequiredKeyedService<IKeyedDecoratedSample>("decorated");
        var unkeyed = provider.GetService<IKeyedDecoratedSample>();

        keyed.Value.ShouldBe("decorated:base");
        unkeyed.ShouldBeNull();
    }

    [Test]
    public void DecoratorsInheritLifetimeFromInnerRegistration()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITransientDecoratedSample>();
        var second = provider.GetRequiredService<ITransientDecoratedSample>();

        first.Id.ShouldNotBe(second.Id);
    }

    public interface IKeyedDecoratedSample
    {
        string Value { get; }
    }

    [DITransient(Key = "decorated", When = [TestCondition])]
    internal sealed class KeyedDecoratedSample : IKeyedDecoratedSample
    {
        public string Value => "base";
    }

    [QudiDecorator(When = [TestCondition])]
    internal sealed partial class KeyedDecoratedSampleDecorator(IKeyedDecoratedSample inner)
        : IKeyedDecoratedSample
    {
        public string Value => $"decorated:{inner.Value}";
    }

    public interface ITransientDecoratedSample
    {
        string Id { get; }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class TransientDecoratedSample : ITransientDecoratedSample
    {
        public string Id { get; } = System.Guid.NewGuid().ToString();
    }

    [QudiDecorator(When = [TestCondition])]
    internal sealed partial class TransientDecoratedSampleDecorator(
        ITransientDecoratedSample inner
    ) : ITransientDecoratedSample
    {
        public string Id { get; } = inner.Id;
    }
}
