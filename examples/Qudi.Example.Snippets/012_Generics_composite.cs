#!/usr/bin/env dotnet
// #:package Qudi@*
// #:package Qudi.Visualizer@*
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
        option.AddOutput("generics-composite.md");
    });
});

var provider = services.BuildServiceProvider();
List<IComponent> components = [battery1, battery2, screen, keyboard];
var validator = provider.GetRequiredService<ComponentValidator>();
foreach (var component in components)
{
    //
    Console.WriteLine($"{component.Name} valid check: {validator.Validate(component)}");
}

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
[QudiComposite]
public partial class ComponentValidatorDispatcher<T> : IComponentValidator<T>
    where T : IComponent
{
    // [CompositeMethod(Result = CompositeResult.Any)]
    // public partial bool Validate(T component);
}

[DITransient]
public class ComponentValidator(IComponentValidator<IComponent> validator)
{
    public bool Validate(IComponent component) => validator.Validate(component);
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
