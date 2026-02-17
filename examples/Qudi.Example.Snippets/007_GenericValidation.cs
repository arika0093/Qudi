#!/usr/bin/env dotnet
// #:package Qudi@*
// #:package Qudi.Visualizer@*
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Qudi.Visualizer;

var services = new ServiceCollection();

services.AddQudiServices(conf =>
{
    conf.EnableVisualizationOutput(option =>
    {
        option.ConsoleOutput = ConsoleDisplay.All;
        option.AddOutput("summary.md");
    });
});

var battery1 = new Battery
{
    Name = "Battery 1",
    Capacity = 6000,
    Voltage = 4,
};
var battery2 = new Battery
{
    Name = "Battery 2",
    Capacity = 4000,
    Voltage = 2,
};
var screen = new Screen { Name = "Screen", Size = 12 };
var keyboard = new Keyboard { Name = "Keyboard", Keys = 104 };

var provider = services.BuildServiceProvider();

// Old way: Get individual IComponentValidator for each component type and call Validate
Console.WriteLine("--- Old Way (Verbose) ---");
var batteryValidator = provider.GetRequiredService<IComponentValidator<Battery>>();
batteryValidator.Validate(battery1);
batteryValidator.Validate(battery2);

var screenValidator = provider.GetRequiredService<IComponentValidator<Screen>>();
screenValidator.Validate(screen);

var keyboardValidator = provider.GetRequiredService<IComponentValidator<Keyboard>>();
keyboardValidator.Validate(keyboard);

// New way: Use ComponentValidator<T> helper for simpler, more discoverable API
Console.WriteLine("\n--- New Way (Simplified with ComponentValidator<T>) ---");
var batteryHelper = provider.GetRequiredService<ComponentValidator<Battery>>();
batteryHelper.Check(battery1);
batteryHelper.Check(battery2);

var screenHelper = provider.GetRequiredService<ComponentValidator<Screen>>();
screenHelper.Check(screen);

var keyboardHelper = provider.GetRequiredService<ComponentValidator<Keyboard>>();
keyboardHelper.Check(keyboard);

// ------ Component Types ------
public interface IComponent { }

public class Battery : IComponent
{
    public string Name { get; set; } = "Battery";
    public int Capacity { get; set; } = 5000;
    public int Voltage { get; set; } = 3;
}

public class Screen : IComponent
{
    public string Name { get; set; } = "Screen";
    public int Size { get; set; } = 6;
}

public class Keyboard : IComponent
{
    public string Name { get; set; } = "Keyboard";
    public int Keys { get; set; } = 104;
}

// ------ Validator Interface ------
public interface IComponentValidator<T>
    where T : IComponent
{
    bool Validate(T component);
}

// ------ Validator Implementations ------
[DITransient]
public class NullComponentValidator<T> : IComponentValidator<T>
    where T : IComponent
{
    public bool Validate(T component)
    {
        Console.WriteLine($"  ✅ {component.GetType().Name}: Using default validator (always valid)");
        return true;
    }
}

[DITransient]
public class BatteryValidator : IComponentValidator<Battery>
{
    public bool Validate(Battery component)
    {
        bool isValid = component.Capacity >= 3000 && component.Voltage >= 3;
        Console.WriteLine(
            $"  {(isValid ? "✅" : "❌")} {component.Name}: Capacity {component.Capacity}mAh, Voltage {component.Voltage}V"
        );
        return isValid;
    }
}

[DITransient]
public class ScreenValidator : IComponentValidator<Screen>
{
    public bool Validate(Screen component)
    {
        bool isValid = component.Size >= 5;
        Console.WriteLine(
            $"  {(isValid ? "✅" : "❌")} {component.Name}: Size {component.Size} inches (minimum: 5 inches)"
        );
        return isValid;
    }
}

// ------ Helper for Simplified API ------
[DITransient]
public class ComponentValidator<T>(IComponentValidator<T> validator)
    where T : IComponent
{
    public bool Check(T component) => validator.Validate(component);
}
