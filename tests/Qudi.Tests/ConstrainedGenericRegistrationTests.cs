using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class ConstrainedGenericRegistrationTests
{
    [Test]
    public void ResolvesConstrainedOpenGeneric()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        var valid = provider.GetRequiredService<ISpecificService<SpecificModel>>();
        valid.ValueType.ShouldBe(typeof(SpecificModel));

        Should.Throw<ArgumentException>(() =>
            provider.GetService<ISpecificService<NonSpecificModel>>()
        );
    }

    [Test]
    public void PrefersSpecificClosedGenericWhenOrderedAfterOpenGeneric()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        var batteryComponent = new Battery();
        var single = provider.GetRequiredService<IComponentValidator<Battery>>();
        single.GetType().ShouldBe(typeof(BatteryValidator));
        single.Validate(batteryComponent).ShouldBeFalse();

        var all = provider.GetServices<IComponentValidator<Battery>>().ToList();
        all.Count.ShouldBe(2);
        all.Any(v => v.GetType() == typeof(BatteryValidator)).ShouldBeTrue();
        all.Any(v => v.GetType() == typeof(NullComponentValidator<Battery>)).ShouldBeTrue();

        var screenComponent = new Screen();
        var screen = provider.GetRequiredService<IComponentValidator<Screen>>();
        screen.GetType().ShouldBe(typeof(NullComponentValidator<Screen>));
        screen.Validate(screenComponent).ShouldBeTrue();
    }

    public interface ISpecificInterface;

    public sealed class SpecificModel : ISpecificInterface;

    public sealed class NonSpecificModel;

    public interface ISpecificService<T>
    {
        System.Type ValueType { get; }
    }

    [DITransient]
    public class SpecificGenericService<T> : ISpecificService<T>
        where T : ISpecificInterface
    {
        public System.Type ValueType => typeof(T);
    }

    public interface IComponent;

    public sealed class Battery : IComponent;

    public sealed class Screen : IComponent;

    public interface IComponentValidator<T>
        where T : IComponent
    {
        bool Validate(T component);
    }

    [DITransient]
    public class NullComponentValidator<T> : IComponentValidator<T>
        where T : IComponent
    {
        public bool Validate(T component) => true;
    }

    [DITransient]
    public class BatteryValidator : IComponentValidator<Battery>
    {
        public bool Validate(Battery component) => false;
    }
}
