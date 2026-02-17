using Microsoft.Extensions.DependencyInjection;
using Qudi;

var battery1 = new Battery { Capacity = 3000, Voltage = 3 };
var battery2 = new Battery { Capacity = 2000, Voltage = 3 };
var screen = new Screen { ResolutionX = 1920, ResolutionY = 1080 };
var keyboard = new Keyboard { KeyCount = 104 };

var services = new ServiceCollection();
services.AddQudiServices(conf =>
{
    conf.EnableVisualizationOutput(option =>
    {
        option.AddOutput("generics-common.md");
    });
});

var provider = services.BuildServiceProvider();
var BatteryValidator = provider.GetRequiredService<ComponentValidator<Battery>>();
Console.WriteLine($"Battery 1 valid check: {BatteryValidator.Validate(battery1)}");
Console.WriteLine($"Battery 2 valid check: {BatteryValidator.Validate(battery2)}");
var ScreenValidator = provider.GetRequiredService<ComponentValidator<Screen>>();
Console.WriteLine($"Screen valid check: {ScreenValidator.Validate(screen)}");
var KeyboardValidator = provider.GetRequiredService<ComponentValidator<Keyboard>>();
Console.WriteLine($"Keyboard valid check: {KeyboardValidator.Validate(keyboard)}");

// -----------
// impl
[DITransient]
public class NullComponentValidator<T> : IComponentValidator<T>
    where T : IComponent
{
    public bool Validate(T component)
    {
        Console.WriteLine($"> No specific validator for {component.Name}, automatically valid.");
        return true;
    }
}

[DITransient]
public class BatteryValidator : IComponentValidator<Battery>
{
    public bool Validate(Battery component)
    {
        Console.WriteLine($"> Validating battery with capacity: {component.Capacity}");
        return component.Capacity >= 3000;
    }
}

[DITransient]
public class BatteryAnotherValidator : IComponentValidator<Battery>
{
    public bool Validate(Battery component)
    {
        Console.WriteLine($"> Validating battery with voltage: {component.Voltage}");
        return component.Voltage >= 3;
    }
}

[DITransient]
public class ScreenValidator : IComponentValidator<Screen>
{
    public bool Validate(Screen component)
    {
        Console.WriteLine(
            $"> Validating screen with resolution: {component.ResolutionX}x{component.ResolutionY}"
        );
        return component.ResolutionX >= 1920 && component.ResolutionY >= 1080;
    }
}

// -----------
// usage
[DITransient]
public class ComponentValidator<T>(IEnumerable<IComponentValidator<T>> validators)
    where T : IComponent
{
    public bool Validate(T component)
    {
        foreach (var validator in validators)
        {
            if (!validator.Validate(component))
            {
                return false;
            }
        }
        return true;
    }
}

// -----------
// decl
public interface IComponentValidator<T>
    where T : IComponent
{
    bool Validate(T component);
}

public interface IComponent
{
    string Name { get; }
}

public class Battery : IComponent
{
    public string Name => "Battery";
    public int Capacity { get; set; }
    public int Voltage { get; set; }
}

public class Screen : IComponent
{
    public string Name => "Screen";
    public int ResolutionX { get; set; }
    public int ResolutionY { get; set; }
}

public class Keyboard : IComponent
{
    public string Name => "Keyboard";
    public int KeyCount { get; set; }
}
