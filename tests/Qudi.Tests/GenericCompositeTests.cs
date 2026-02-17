using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

/// <summary>
/// Tests for generic composite feature enhancement
/// </summary>
public sealed partial class GenericCompositeTests
{
    [Test]
    public void ValidateListOfComponentsThroughGenericValidator()
    {
        // Feature Request 1: Validate a list of IComponent through ComponentValidator<T> in bulk
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        var battery1 = new Battery { Name = "Battery 1", Capacity = 6000, Voltage = 4 };
        var battery2 = new Battery { Name = "Battery 2", Capacity = 4000, Voltage = 2 };
        var screen = new Screen { Name = "Screen", Size = 12 };
        var keyboard = new Keyboard { Name = "Keyboard", Keys = 104 };

        // Get ComponentValidator for specific types
        var batteryValidator = provider.GetRequiredService<ComponentValidator<Battery>>();
        batteryValidator.Check(battery1).ShouldBeTrue(); // both validators pass
        batteryValidator.Check(battery2).ShouldBeFalse(); // one validator fails (voltage)

        var screenValidator = provider.GetRequiredService<ComponentValidator<Screen>>();
        screenValidator.Check(screen).ShouldBeTrue();

        var keyboardValidator = provider.GetRequiredService<ComponentValidator<Keyboard>>();
        keyboardValidator.Check(keyboard).ShouldBeTrue(); // uses null validator
    }

    [Test]
    public void GenericCompositeValidatorWorksWithPartialClass()
    {
        // Feature Request 3: Use Composite feature for generics
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        var battery = new Battery { Name = "Battery", Capacity = 6000, Voltage = 4 };

        // Get the composite validator
        var compositeValidator = provider.GetRequiredService<IComponentValidator<Battery>>();
        compositeValidator.ShouldBeOfType<CompositeValidator<Battery>>();

        // Should call all validators
        var result = compositeValidator.Validate(battery);
        result.ShouldBeTrue(); // all validators pass
    }

    [Test]
    public void GenericCompositeValidatorAggregatesWithAnd()
    {
        // Feature Request 3: Composite with generic types
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        var batteryValid = new Battery { Name = "Good Battery", Capacity = 6000, Voltage = 4 };
        var batteryInvalid = new Battery { Name = "Bad Battery", Capacity = 4000, Voltage = 2 };

        var compositeValidator = provider.GetRequiredService<IComponentValidator<Battery>>();

        // All validators must pass
        compositeValidator.Validate(batteryValid).ShouldBeTrue();

        // If any validator fails, composite fails
        compositeValidator.Validate(batteryInvalid).ShouldBeFalse();
    }

    // Test components
    public interface IComponent
    {
        string Name { get; }
    }

    public class Battery : IComponent
    {
        public required string Name { get; set; }
        public int Capacity { get; set; }
        public int Voltage { get; set; }
    }

    public class Screen : IComponent
    {
        public required string Name { get; set; }
        public int Size { get; set; }
    }

    public class Keyboard : IComponent
    {
        public required string Name { get; set; }
        public int Keys { get; set; }
    }

    // Validator interface
    public interface IComponentValidator<T>
        where T : IComponent
    {
        bool Validate(T component);
    }

    // Default (fallback) implementation
    [DITransient]
    public class NullComponentValidator<T> : IComponentValidator<T>
        where T : IComponent
    {
        public bool Validate(T component) => true; // default to valid
    }

    // Specialized implementation for Battery
    [DITransient]
    public class BatteryValidator : IComponentValidator<Battery>
    {
        public bool Validate(Battery component) => component.Capacity > 5000;
    }

    [DITransient]
    public class BatteryAnotherValidator : IComponentValidator<Battery>
    {
        public bool Validate(Battery component) => component.Voltage > 3;
    }

    // Specialized implementation for Screen
    [DITransient]
    public class ScreenValidator : IComponentValidator<Screen>
    {
        public bool Validate(Screen component) => component.Size > 10;
    }

    // ComponentValidator that aggregates all validators
    // NOTE: This is NOT registered as IComponentValidator<T> to avoid circular dependency
    [DITransient]
    public class ComponentValidator<T>(IEnumerable<IComponentValidator<T>> validators)
        where T : IComponent
    {
        public bool Check(T component)
        {
            foreach (var validator in validators)
            {
                if (!validator.Validate(component))
                {
                    return false; // validation failed
                }
            }
            return true; // all validations passed
        }
    }

    // Generic composite validator using QudiComposite
    [QudiComposite]
    public partial class CompositeValidator<T>(IEnumerable<IComponentValidator<T>> innerServices)
        : IComponentValidator<T>
        where T : IComponent;
}
