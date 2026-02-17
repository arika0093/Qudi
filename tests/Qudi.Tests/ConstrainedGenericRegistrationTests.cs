using System;
using System.Collections.Generic;
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
        all.Count.ShouldBe(1);
        all.Any(v => v.GetType() == typeof(BatteryValidator)).ShouldBeTrue();

        var keyboardAll = provider.GetServices<IComponentValidator<Keyboard>>().ToList();
        keyboardAll.Count.ShouldBe(1);
        keyboardAll[0].GetType().ShouldBe(typeof(NullComponentValidator<Keyboard>));

        var screenComponent = new Screen();
        var screen = provider.GetRequiredService<IComponentValidator<Screen>>();
        screen.GetType().ShouldBe(typeof(ScreenValidator));
        screen.Validate(screenComponent).ShouldBeFalse();
    }

    [Test]
    public void MaterializesOpenGenericFallbackOnlyForTypesWithoutConcreteImplementation()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<ComponentValidationConsumer>();

        consumer.BatteryValidators.Count.ShouldBe(1);
        consumer.BatteryValidators[0].GetType().ShouldBe(typeof(BatteryValidator));

        consumer.ScreenValidators.Count.ShouldBe(1);
        consumer.ScreenValidators[0].GetType().ShouldBe(typeof(ScreenValidator));

        consumer.KeyboardValidators.Count.ShouldBe(1);
        consumer.KeyboardValidators[0].GetType().ShouldBe(typeof(NullComponentValidator<Keyboard>));
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

    public sealed class Keyboard : IComponent;

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

    [DITransient]
    public class ScreenValidator : IComponentValidator<Screen>
    {
        public bool Validate(Screen component) => false;
    }

    [DITransient]
    public sealed class ComponentValidationConsumer(
        IEnumerable<IComponentValidator<Battery>> batteryValidators,
        IEnumerable<IComponentValidator<Screen>> screenValidators,
        IEnumerable<IComponentValidator<Keyboard>> keyboardValidators
    )
    {
        public List<IComponentValidator<Battery>> BatteryValidators { get; } =
            batteryValidators.ToList();

        public List<IComponentValidator<Screen>> ScreenValidators { get; } =
            screenValidators.ToList();

        public List<IComponentValidator<Keyboard>> KeyboardValidators { get; } =
            keyboardValidators.ToList();
    }

    [Test]
    public void ComponentValidatorHelperSimplifiesValidationAPI()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        // Verify ComponentValidator<T> is registered and works
        var batteryValidator = provider.GetRequiredService<ComponentValidator<Battery>>();
        var screenValidator = provider.GetRequiredService<ComponentValidator<Screen>>();
        var keyboardValidator = provider.GetRequiredService<ComponentValidator<Keyboard>>();

        batteryValidator.ShouldNotBeNull();
        screenValidator.ShouldNotBeNull();
        keyboardValidator.ShouldNotBeNull();

        var battery = new Battery();
        var screen = new Screen();
        var keyboard = new Keyboard();

        batteryValidator.Check(battery).ShouldBeFalse();
        screenValidator.Check(screen).ShouldBeFalse();
        keyboardValidator.Check(keyboard).ShouldBeTrue();
    }

    [DITransient]
    public class ComponentValidator<T>(IComponentValidator<T> validator)
        where T : IComponent
    {
        public bool Check(T component) => validator.Validate(component);
    }
}
