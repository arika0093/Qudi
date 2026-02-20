using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class ServiceLifetimeTests
{
    private const string TestCondition = nameof(ServiceLifetimeTests);

    [Test]
    public void RegistersSingletonAsSameInstance()
    {
        using var provider = BuildProvider();
        var first = provider.GetRequiredService<ISingletonSample>();
        var second = provider.GetRequiredService<ISingletonSample>();

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Test]
    public void RegistersTransientAsDifferentInstances()
    {
        using var provider = BuildProvider();
        var first = provider.GetRequiredService<ITransientSample>();
        var second = provider.GetRequiredService<ITransientSample>();

        ReferenceEquals(first, second).ShouldBeFalse();
    }

    [Test]
    public void RegistersScopedAsSameInstancePerScope()
    {
        using var provider = BuildProvider();

        using var scope1 = provider.CreateScope();
        var scope1First = scope1.ServiceProvider.GetRequiredService<IScopedSample>();
        var scope1Second = scope1.ServiceProvider.GetRequiredService<IScopedSample>();

        using var scope2 = provider.CreateScope();
        var scope2First = scope2.ServiceProvider.GetRequiredService<IScopedSample>();

        ReferenceEquals(scope1First, scope1Second).ShouldBeTrue();
        ReferenceEquals(scope1First, scope2First).ShouldBeFalse();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));
        return services.BuildServiceProvider();
    }

    internal interface ISingletonSample
    {
        string Name { get; }
    }

    [DISingleton(When = [TestCondition])]
    internal sealed class SingletonSample : ISingletonSample
    {
        public string Name => "singleton";
    }

    internal interface ITransientSample
    {
        string Name { get; }
    }

    [DITransient(When = [TestCondition])]
    internal sealed class TransientSample : ITransientSample
    {
        public string Name => "transient";
    }

    internal interface IScopedSample
    {
        string Name { get; }
    }

    [DIScoped(When = [TestCondition])]
    internal sealed class ScopedSample : IScopedSample
    {
        public string Name => "scoped";
    }
}
