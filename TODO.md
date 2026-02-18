## Issues
I need your help with the following content:

- Attribute-based registration itself is an anti-pattern because it creates a dependency on a specific library.
    - Collecting via attributes may be unavoidable by nature, but the registration process could be handled in the Add... methods.
- Duplicate handling and registration strategy. See: https://github.com/loresoft/Injectio
- Enable ValidateOnBuild by default in MS.DI
- Specifying order (Order) is also questionable...
- memo: https://www.reddit.com/r/csharp/comments/1jqgslc/attribute_based_di_autoregistration/

## Generics
### G001: Multiple Implementations with Generics
By resolving `IComponentValidator<IComponent>`, we can perform validation for all components sequentially as shown below.

```csharp
var validator = provider.GetRequiredService<IComponentValidator<IComponent>>();
List<IComponent> components = [battery1, battery2, screen, keyboard];
foreach (var component in components)
{
    // in Battery, called [BatteryValidator, BatteryAnotherValidator]
    // in Screen, called [ScreenValidator]
    // in Keyboard, called [NullComponentValidator<Keyboard>]
    Console.WriteLine($"{component.Name} valid check: {validator.Validate(component)}");
}

// impl
public class NullComponentValidator<T> : IComponentValidator<T> where T : IComponent { /*...*/}
public class BatteryValidator : IComponentValidator<Battery> { /*...*/ }
public class BatteryAnotherValidator : IComponentValidator<Battery> { /*...*/ }
public class ScreenValidator : IComponentValidator<Screen> { /*...*/ }

// decl
public interface IComponentValidator<T> where T : IComponent
{
    bool Validate(T component);
}
public interface IComponent
{
    string Name { get; }
}
```

### G002: Visualization of Generic Registrations
By registering as shown below, we can visualize the connections to each `IComponentValidator<T>`.

```csharp
public class ComponentValidator<T>(IEnumerable<IComponentValidator<T>> validators)
    where T : IComponent
{
    public bool Validate(T component)
    {
        foreach (var validator in validators)
        {
            if (!validator.Validate(component)) return false;
        }
        return true;
    }
}

// registration graph will be like below:
// ComponentValidator<T>
//  --> IComponentValidator<Battery>
//     --> BatteryValidator
//     --> BatteryAnotherValidator
//  --> IComponentValidator<Screen>
//     --> ScreenValidator
//  --> IComponentValidator<Keyboard>
//     --> NullComponentValidator<Keyboard>
```

### G003: Composite support for Generics
By using the `ComponentValidator<T>` as a composite, we can perform validation for each component as shown below.

```csharp
[QudiComposite]
public partial class ComponentValidator<T>(IEnumerable<IComponentValidator<T>> validators)
    where T : IComponent
{
    // will be automatically generated Validate method that calls all validators in the collection
}

// and then we can resolve the composite for each component type as shown below:
var validator = provider.GetRequiredService<IComponentValidator<IComponent>>(); // -> should be ComponentValidator<IComponent>
List<IComponent> components = [battery1, battery2, screen, keyboard];
foreach (var component in components)
{
    Console.WriteLine($"{component.Name} valid check: {validator.Validate(component)}");
}
```

## Composite
### C001: Improve automatic generation of implementations
The current implementation is as shown below, but with this implementation generation, it is difficult to handle the results from the parent side.

```csharp
[QudiComposite]
public partial class MyComposite(IEnumerable<IService> services) : IService;

// will be generated
partial class MyComposite(IEnumerable<IService> services) : IService_MyComposite
{
    IEnumerable<IService> IService_MyComposite.__InterServices => services;
}
public interface IService_MyComposite : IService
{
    IEnumerable<IService> __InterServices { get; }

    void IService.Method()
    {
        foreach (var service in __InterServices)
        {
            service.Method();
        }
    }
}
```

To make it easier to handle results from the parent side, we can generate code as shown below. If there are methods that require customization, they can be declared as partial, and the implementation will be generated based on the specified result type (e.g., All, Any). If there are methods or properties that are not declared, they will be automatically generated to throw exceptions.

```csharp
[QudiComposite]
public partial class MyComposite(IEnumerable<IService> services) : IService
{
    // If customization is not needed, declaration is not required as before
    // void MethodA();

    // If customization is needed, declare it as partial (implementation not required)
    [CompositeMethod(Result = CompositeResult.All)]
    public partial bool MethodB();

    [CompositeMethod(Result = CompositeResult.Any)]
    public partial bool MethodC();

    // If methods or properties exist that are not in the above list, they will be automatically generated to throw exceptions.
    // For example:
    // * int ErrorA()
    // * string ErrorB(int val)
    // * string PropertyC { get; }
    // * double PropertyD { get; set; }
}

// generated code
partial class MyComposite(IEnumerable<IService> services) : IService_MyComposite
{
    IEnumerable<IService> IService_MyComposite.__InterServices => services;

    // If there is no [CompositeMethod], the implementation of the interface will be used as before

    // If there is a [CompositeMethod], an implementation will be generated according to the Result and will override the implementation of the interface
    public partial bool MethodB()
    {
        foreach (var service in __InterServices)
        {
            if (!service.MethodB()) return false;
        }
        return true;
    }

    public partial bool MethodC()
    {
        foreach (var service in __InterServices)
        {
            if (service.MethodC()) return true;
        }
        return false;
    }
}

public interface IService_MyComposite : IService
{
    IEnumerable<IService> __InterServices { get; }

    // Provide a standard implementation according to IService

    // for example, if void, just call all
    void IService.MethodA()
    {
        foreach (var service in __InterServices)
        {
            service.MethodA();
        }
    }

    // if bool, generate as All
    bool IService.MethodB()
    {
        foreach (var service in __InterServices)
        {
            if (!service.MethodB()) return false;
        }
        return true;
    }

    // if Task, generate as All
    Task IService.MethodC()
    {
        return Task.WhenAll(__InterServices.Select(s => s.MethodC()));
    }

    //  For properties and methods that return int/string, generate an implementation that throws an exception
    int IService.ErrorA()
    {
        throw new NotSupportedException("ErrorA is not supported in MyComposite.");
    }
    string IService.ErrorB(int val)
    {
        throw new NotSupportedException("ErrorB is not supported in MyComposite.");
    }
    string IService.PropertyC => throw new NotSupportedException("PropertyC is not supported in MyComposite.");
    double IService.PropertyD
    {
        get => throw new NotSupportedException("PropertyD is not supported in MyComposite.");
        set => throw new NotSupportedException("PropertyD is not supported in MyComposite.");
    }
}
```

Currently, the contents of Decorator/Composite are mixed in Helper/HelperCodeGenerator, but we will separate them.