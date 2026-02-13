using Microsoft.Extensions.DependencyInjection;
using Qudi;

namespace Qudi.Examples.GenericRegistration;

public interface IComponent { }

public class Battery : IComponent
{
    public int Capacity { get; set; } = 5000;
}

public class Screen : IComponent
{
    public int Size { get; set; } = 6;
}

public class Keyboard : IComponent
{
    public int Keys { get; set; } = 104;
}

public interface IComponentValidator<T> where T : IComponent
{
    bool Validate(T component);
}

// default(fallback) implementation
[DITransient]
public class NullComponentValidator<T> : IComponentValidator<T> where T : IComponent
{
    public bool Validate(T component)
    {
        Console.WriteLine($"  ✅ {typeof(T).Name}: Using default validator (always valid)");
        return true;
    }
}

// specialized implementation for Battery
[DITransient]
public class BatteryValidator : IComponentValidator<Battery>
{
    public bool Validate(Battery component)
    {
        bool isValid = component.Capacity >= 3000;
        Console.WriteLine($"  {(isValid ? "✅" : "❌")} Battery: Capacity {component.Capacity}mAh (minimum: 3000mAh)");
        return isValid;
    }
}

// specialized implementation for Screen
[DITransient]
public class ScreenValidator : IComponentValidator<Screen>
{
    public bool Validate(Screen component)
    {
        bool isValid = component.Size >= 5;
        Console.WriteLine($"  {(isValid ? "✅" : "❌")} Screen: Size {component.Size} inches (minimum: 5 inches)");
        return isValid;
    }
}

[DISingleton(Export = true)]
public class GenericRegistrationExecutor(
    IEnumerable<IComponentValidator<Battery>> batteryValidators,
    IEnumerable<IComponentValidator<Screen>> screenValidators,
    IEnumerable<IComponentValidator<Keyboard>> keyboardValidators) : ISampleExecutor
{
    public string Name => "Generic Registration";
    public string Description => "Open generic types with specialized implementations";
    public string Namespace => typeof(GenericRegistrationExecutor).Namespace!;

    public void Execute()
    {
        Console.WriteLine("Validating components:");

        // Get specialized validator for Battery
        var batteryValidator = batteryValidators.Last();
        batteryValidator.Validate(new Battery { Capacity = 5000 });

        // Get specialized validator for Screen
        var screenValidator = screenValidators.Last();
        screenValidator.Validate(new Screen { Size = 6 });

        // Get default validator for Keyboard (no specialized implementation)
        var keyboardValidator = keyboardValidators.Last();
        keyboardValidator.Validate(new Keyboard { Keys = 104 });
    }
}
