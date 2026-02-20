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

public interface IComponentValidator<T>
    where T : IComponent
{
    bool Validate(T component);
}

// default(fallback) implementation
[DITransient]
public class NullComponentValidator<T> : IComponentValidator<T>
    where T : IComponent
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
        Console.WriteLine(
            $"  {(isValid ? "✅" : "❌")} Battery: Capacity {component.Capacity}mAh (minimum: 3000mAh)"
        );
        return isValid;
    }
}

// another implementation for Battery
[DITransient]
public class BatteryAnotherValidator : IComponentValidator<Battery>
{
    public bool Validate(Battery component)
    {
        bool isValid = component.Capacity % 2 == 0;
        Console.WriteLine(
            $"  {(isValid ? "✅" : "❌")} Battery: Capacity {component.Capacity}mAh is even"
        );
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
        Console.WriteLine(
            $"  {(isValid ? "✅" : "❌")} Screen: Size {component.Size} inches (minimum: 5 inches)"
        );
        return isValid;
    }
}

[QudiDispatch]
public partial class ComponentValidatorDispatcher : IComponentValidator<IComponent>
{
    // The generator will route calls to the appropriate IComponentValidator<T> based on the runtime type of the argument.
    // If multiple validators are registered for a type, it will resolve all of them and call them in sequence.
    // If no validators are registered for a type, it will use the NullComponentValidator<T> fallback implementation.
}

[DITransient(Export = true)]
public class GenericRegistrationExecutor(IComponentValidator<IComponent> componentValidator)
    : ISampleExecutor
{
    public string Name => "Generic Registration";
    public string Description =>
        "Open generic types with specialized implementations and use [QudiDispatch].";
    public string Namespace => typeof(GenericRegistrationExecutor).Namespace!;

    public void Execute()
    {
        Console.WriteLine("Validating components:");

        var components = new IComponent[]
        {
            new Battery { Capacity = 5000 },
            new Battery { Capacity = 2500 },
            new Battery { Capacity = 5555 },
            new Screen { Size = 6 },
            new Screen { Size = 4 },
            new Keyboard { Keys = 104 },
        };

        foreach (var component in components)
        {
            var rst = componentValidator.Validate(component);
            Console.WriteLine(
                $"  --> Overall validation result: {(rst ? "✅ Valid" : "❌ Invalid")}\n"
            );
        }
    }
}
