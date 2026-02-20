using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Qudi.Tests;

public sealed partial class ComplexDispatcherTests
{
    private const string NormalDispatch = nameof(NormalDispatcherTest);
    private const string AnyDispatch = nameof(AnyDispatcherTest);
    private const string WithDecorator = nameof(WithDecoratorTest);

    [Test]
    public void NormalDispatcherTest()
    {
        var b1 = new Battery { Capacity = 3500, Voltage = 3 };
        var b2 = new Battery { Capacity = 3500, Voltage = 2 };
        var b3 = new Battery { Capacity = 2525, Voltage = 4 };
        var s1 = new Screen { ResolutionX = 2560, ResolutionY = 1440 };
        var s2 = new Screen { ResolutionX = 1280, ResolutionY = 720 };
        var k1 = new Keyboard { KeyCount = 104, TestResult = true };
        var k2 = new Keyboard { KeyCount = 61, TestResult = true };

        using var provider = BuildProvider(NormalDispatch);
        var validator = provider.GetRequiredService<ComponentValidator>();
        var logger = provider.GetRequiredService<ExecuteCheckLogger>();

        // b1
        validator.Validate(b1).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(BatteryValidator), nameof(BatteryAnotherValidator)]);
        logger.Clear();

        // b2
        validator.Validate(b2).ShouldBeFalse();
        logger.Logs.ShouldBe([nameof(BatteryValidator), nameof(BatteryAnotherValidator)]);
        logger.Clear();

        // b3
        validator.Validate(b3).ShouldBeFalse();
        logger.Logs.ShouldBe([nameof(BatteryValidator)]);
        logger.Clear();

        // s1
        validator.Validate(s1).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(ScreenValidator)]);
        logger.Clear();

        // s2
        validator.Validate(s2).ShouldBeFalse();
        logger.Logs.ShouldBe([nameof(ScreenValidator)]);
        logger.Clear();

        // k1
        validator.Validate(k1).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(NullComponentValidator<>)]);
        logger.Clear();

        // k2
        validator.Validate(k2).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(NullComponentValidator<>)]);
        logger.Clear();
    }

    [Test]
    public void AnyDispatcherTest()
    {
        var b1 = new Battery { Capacity = 3500, Voltage = 3 };
        var b2 = new Battery { Capacity = 3500, Voltage = 2 };
        var b3 = new Battery { Capacity = 2525, Voltage = 4 };
        var s1 = new Screen { ResolutionX = 2560, ResolutionY = 1440 };
        var s2 = new Screen { ResolutionX = 1280, ResolutionY = 720 };
        var k1 = new Keyboard { KeyCount = 104, TestResult = true };
        var k2 = new Keyboard { KeyCount = 61, TestResult = true };

        using var provider = BuildProvider(AnyDispatch);
        var validator = provider.GetRequiredService<ComponentValidator>();
        var logger = provider.GetRequiredService<ExecuteCheckLogger>();

        // b1
        validator.Validate(b1).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(BatteryValidator)]);
        logger.Clear();

        // b2
        validator.Validate(b2).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(BatteryValidator)]);
        logger.Clear();

        // b3
        validator.Validate(b3).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(BatteryValidator), nameof(BatteryAnotherValidator)]);
        logger.Clear();

        // s1
        validator.Validate(s1).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(ScreenValidator)]);
        logger.Clear();

        // s2
        validator.Validate(s2).ShouldBeFalse();
        logger.Logs.ShouldBe([nameof(ScreenValidator)]);
        logger.Clear();

        // k1
        validator.Validate(k1).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(NullComponentValidator<>)]);
        logger.Clear();

        // k2
        validator.Validate(k2).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(NullComponentValidator<>)]);
        logger.Clear();
    }

    [Test]
    public void WithDecoratorTest()
    {
        var b1 = new Battery { Capacity = 3500, Voltage = 3 };
        var b2 = new Battery { Capacity = 3500, Voltage = 2 };
        var b3 = new Battery { Capacity = 2525, Voltage = 4 };
        var s1 = new Screen { ResolutionX = 2560, ResolutionY = 1440 };
        var s2 = new Screen { ResolutionX = 1280, ResolutionY = 720 };
        var k1 = new Keyboard { KeyCount = 104, TestResult = true };
        var k2 = new Keyboard { KeyCount = 61, TestResult = true };

        using var provider = BuildProvider(WithDecorator);
        var validator = provider.GetRequiredService<ComponentValidator>();
        var logger = provider.GetRequiredService<ExecuteCheckLogger>();

        // b1
        validator.Validate(b1).ShouldBeTrue();
        logger.Logs.ShouldBe([
            nameof(ComponentValidatorDecorator),
            nameof(BatteryValidator),
            nameof(BatteryAnotherValidator),
        ]);
        logger.Clear();

        // b2
        validator.Validate(b2).ShouldBeFalse();
        logger.Logs.ShouldBe([
            nameof(ComponentValidatorDecorator),
            nameof(BatteryValidator),
            nameof(BatteryAnotherValidator),
        ]);
        logger.Clear();

        // b3
        validator.Validate(b3).ShouldBeFalse();
        logger.Logs.ShouldBe([nameof(ComponentValidatorDecorator), nameof(BatteryValidator)]);
        logger.Clear();

        // s1
        validator.Validate(s1).ShouldBeTrue();
        logger.Logs.ShouldBe([nameof(ComponentValidatorDecorator), nameof(ScreenValidator)]);
        logger.Clear();

        // s2
        validator.Validate(s2).ShouldBeFalse();
        logger.Logs.ShouldBe([nameof(ComponentValidatorDecorator), nameof(ScreenValidator)]);
        logger.Clear();

        // k1
        validator.Validate(k1).ShouldBeTrue();
        logger.Logs.ShouldBe([
            nameof(ComponentValidatorDecorator),
            nameof(NullComponentValidator<>),
        ]);
        logger.Clear();

        // k2
        validator.Validate(k2).ShouldBeTrue();
        logger.Logs.ShouldBe([
            nameof(ComponentValidatorDecorator),
            nameof(NullComponentValidator<>),
        ]);
        logger.Clear();
    }

    private static ServiceProvider BuildProvider(string condition)
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(condition));
        return services.BuildServiceProvider();
    }

    // -----------
    // impl
    [DITransient(When = [NormalDispatch, AnyDispatch, WithDecorator])]
    internal class NullComponentValidator<T>(ExecuteCheckLogger logger) : IComponentValidator<T>
        where T : IComponent
    {
        public bool Validate(T component)
        {
            logger.Log(nameof(NullComponentValidator<>));
            return true;
        }
    }

    [DITransient(When = [NormalDispatch, AnyDispatch, WithDecorator])]
    internal class BatteryValidator(ExecuteCheckLogger logger) : IComponentValidator<Battery>
    {
        public bool Validate(Battery component)
        {
            logger.Log(nameof(BatteryValidator));
            return component.Capacity >= 3000;
        }
    }

    [DITransient(When = [NormalDispatch, AnyDispatch, WithDecorator])]
    internal class BatteryAnotherValidator(ExecuteCheckLogger logger) : IComponentValidator<Battery>
    {
        public bool Validate(Battery component)
        {
            logger.Log(nameof(BatteryAnotherValidator));
            return component.Voltage >= 3;
        }
    }

    [DITransient(When = [NormalDispatch, AnyDispatch, WithDecorator])]
    internal class ScreenValidator(ExecuteCheckLogger logger) : IComponentValidator<Screen>
    {
        public bool Validate(Screen component)
        {
            logger.Log(nameof(ScreenValidator));
            return component.ResolutionX >= 1920 && component.ResolutionY >= 1080;
        }
    }

    // -----------
    [QudiDispatch(When = [NormalDispatch, WithDecorator])]
    internal partial class ComponentAllValidatorDispatcher : IComponentValidator<IComponent>;

    [QudiDispatch(When = [AnyDispatch])]
    internal partial class ComponentAnyValidatorDispatcher : IComponentValidator<IComponent>
    {
        [CompositeMethod(Result = CompositeResult.Any)]
        public partial bool Validate(IComponent component);
    }

    [QudiDecorator(When = [WithDecorator])]
    internal partial class ComponentValidatorDecorator(
        IComponentValidator<IComponent> decorated,
        ExecuteCheckLogger logger
    ) : IComponentValidator<IComponent>
    {
        public bool Validate(IComponent component)
        {
            logger.Log(nameof(ComponentValidatorDecorator));
            var result = decorated.Validate(component);
            return result;
        }
    }

    [DITransient(When = [NormalDispatch, AnyDispatch, WithDecorator])]
    internal class ComponentValidator(IComponentValidator<IComponent> validator)
    {
        public bool Validate(IComponent component) => validator.Validate(component);
    }

    [DISingleton(When = [NormalDispatch, AnyDispatch, WithDecorator])]
    internal class ExecuteCheckLogger
    {
        public List<string> Logs { get; set; } = [];

        public void Log(string message) => Logs.Add(message);

        public void Clear() => Logs.Clear();
    }

    // -----------
    // decl
    internal interface IComponentValidator<T>
        where T : IComponent
    {
        bool Validate(T component);
    }

    internal interface IComponent
    {
        string Name { get; }
    }

    internal class Battery : IComponent
    {
        public string Name => "Battery";
        public bool TestResult { get; set; }
        public int Capacity { get; set; }
        public int Voltage { get; set; }
    }

    internal class Screen : IComponent
    {
        public string Name => "Screen";
        public bool TestResult { get; set; }
        public int ResolutionX { get; set; }
        public int ResolutionY { get; set; }
    }

    internal class Keyboard : IComponent
    {
        public string Name => "Keyboard";
        public bool TestResult { get; set; }
        public int KeyCount { get; set; }
    }
}
