# Qudi
[![NuGet Version](https://img.shields.io/nuget/v/Qudi?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Qudi/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Qudi/test.yaml?branch=main&label=Test&style=flat-square) 

**Qudi** (`/kʲɯːdiː/`, Quickly Dependency Injection) is an attribute-based dependency injection helper library.  
Explicitly, No assembly scan, AOT friendly.

## Quick Start
### Overview
Well, it's easier to show you than to explain it. 😉

```csharp
#!/usr/bin/env dotnet
#:package Qudi@*
using Microsoft.Extensions.DependencyInjection;
using Qudi;

var services = new ServiceCollection();
// ✅️ register services marked with Qudi attributes (see below)
services.AddQudiServices();

var provider = services.BuildServiceProvider();
var pokemons = provider.GetServices<IPokemon>();
foreach (var pokemon in pokemons)
{
    pokemon.DisplayInfo();
}

// ------ Declare services ------
public interface IPokemon
{
    string Name { get; }
    IEnumerable<string> Types { get; }
    public void DisplayInfo() =>
        Console.WriteLine($"{Name} is a {string.Join("/", Types)} type Pokémon.");
}

[DISingleton] // ✅️ mark as singleton
public class Altaria : IPokemon
{
    public string Name => "Altaria";
    public IEnumerable<string> Types => ["Dragon", "Flying"];
}

[DITransient] // ✅️ mark as transient, too
public class Abomasnow : IPokemon
{
    public string Name => "Abomasnow";
    public IEnumerable<string> Types => ["Grass", "Ice"];
}

// Output:
// > Altaria is a Dragon/Flying type Pokémon.
// > Abomasnow is a Grass/Ice type Pokémon.
```

As you can see, just these two steps. 

1. Mark each class with attributes like `[DISingleton]`, `[DITransient]`, etc.
2. Call `IServiceCollection.AddQudiServices`.

When written like this, the following equivalent code is automatically generated and registered in the DI container:

```csharp
public IServiceCollection AddQudiServices(this IServiceCollection services, Action<QudiConfiguration>? configuration = null)
{
    // Generated code similar to this:
    services.AddSingleton<Altaria>();
    services.AddTransient<Abomasnow>();
    services.AddSingleton<IPokemon, Altaria>(provider => provider.GetRequiredService<Altaria>());
    services.AddTransient<IPokemon, Abomasnow>(provider => provider.GetRequiredService<Abomasnow>());
    return services;
}
```

Want to know more about the internal behavior? See the [Architecture](#architecture) section.

### Installation
Install `Qudi` from NuGet.

```bash
dotnet add package Qudi
```

Alternatively, you can install `Qudi.Core` and `Qudi.Container.*` packages separately.

```bash
# install Qudi.Core (common models and source generator)
dotnet add package Qudi.Core
# install container-specific package (here, Microsoft.Extensions.DependencyInjection)
dotnet add package Qudi.Container.Microsoft
```

<details>
<summary>What is the difference between Qudi and Qudi.Core ?</summary>

`Qudi` is a meta-package that combines `Qudi.Core` and `Qudi.Container.Microsoft`.  
Additionally, there is a difference in whether `Qudi.Generator` is exposed externally.

* `Qudi`: Dependent projects/libraries can also use `Qudi.Generator`.
    * In a dependency chain like `Qudi` -> `A` -> `B`, `B` can also use `Qudi.Generator`.
* `Qudi.Core`: Marked as a development-time dependency, so other projects/libraries cannot use `Qudi.Generator`.
    * In a dependency chain like `Qudi.Core` -> `A` -> `B`, `B` cannot use `Qudi.Generator`.

Which is preferable depends on your situation.  
For monorepo projects you don't intend to publish externally, using `Qudi` is convenient.  
When publishing as a library, using `Qudi.Core` allows you to avoid forcing the source generator on your users.

</details>

### Benefits
Compared to [Scrutor](https://github.com/khellang/Scrutor), the advantages of this library (in my opinion) are as follows:

* **Explicit**: Registration is controlled using attributes. While this style depends on preference, I prefer explicit registration.
* **Conditional Registration**: Conditional registration is possible via attribute parameters. Of course, Scrutor can do this too, but Qudi makes it easier to achieve.
* **No Assembly Scan**: No assembly scanning. It works in AOT environments and is very fast.

## Various Usages
### In Multiple Projects
Dependency Injection is often performed across multiple projects in a solution.  
For example, consider a case where code implemented inside a Core project is used from another project via an interface.

```csharp
// MyApp.Core ----------------
// Shared interface
public interface IDataRepository
{
    Task<MyData> GetDataAsync(int id);
}
// Implementation in MyApp.Core, this class is internal!
internal class SqlDataRepository : IDataRepository
{
    public Task<MyData> GetDataAsync(int id)
    {
        // fetch data from SQL database
    }
}

// MyApp.Web -------------
internal class MyService(IDataRepository repository)
{
    public async Task DoSomethingAsync(int id)
    {
        var data = await repository.GetDataAsync(id);
        // do something with data
    }
}
```

In this case, introduce `Qudi` to `MyApp.Core` (or install `Qudi.Core` to all projects).  
Next, mark the implementation class and the dependent class with Qudi attributes.

```csharp
// in MyApp.Core
[DISingleton]
internal class SqlDataRepository : IDataRepository { /* ... */ }

// in MyApp.Web
[DITransient]
internal class MyService(IDataRepository repository) { /* ... */ }
```

Then, just call `AddQudiServices` as usual in the startup code of `MyApp.Web`.

```csharp
// in MyApp.Web
services.AddQudiServices();
```

If you don't want to register implementations from other libraries, you can specify it explicitly in `AddQudiServices`.

```csharp
services.AddQudiServices(conf => {
    conf.UseSelfImplementsOnly();
});
```

### Conditional Registration
For example, consider a case where you want to use a mock implementation in the development environment and the actual implementation in the production environment.
In this case, you can specify the environment with attributes as follows.

```csharp
public interface IPaymentService
{
    void ProcessPayment(decimal amount);
}

[DITransient(When = [Condition.Development])]
public class MockPaymentService : IPaymentService
{
    public void ProcessPayment(decimal amount)
    {
        Console.WriteLine($"[Mock] Processed payment of {amount:C}");
    }
}

[DITransient(When = [Condition.Production])]
public class RealPaymentService : IPaymentService
{
    public void ProcessPayment(decimal amount)
    {
        // Actual payment processing logic
    }
}

// you can add customized condition key
// [DITransient(When = ["testing"])]
```

then, specify the rules to apply each condition as an argument of the `AddQudiServices` method.

```csharp
builder.Services.AddQudiServices(conf => {
    // Detection from IHostEnvironment
    conf.SetCondition(builder.Environment.EnvironmentName);
    // Or set it directly
    conf.SetCondition(Condition.Development);
    conf.SetCondition("testing");
    // Alternatively, you can set conditions based on environment variables
    conf.SetConditionFromEnvironment("ASPNETCORE_ENVIRONMENT");
});
```

### Decorator Pattern
Decorator pattern is a useful technique to add functionality to existing services without modifying their code.
You can easily register decorator classes using the `[QudiDecorator]` attribute.

```csharp
[QudiDecorator(Lifetime = Lifetime.Singleton, Order = 1)]
public class LoggingMessageServiceDecorator(
    IMessageService innerService,
    ILogger<LoggingMessageServiceDecorator> logger
) : IMessageService
{
    public void SendMessage(string message)
    {
        logger.LogTrace("Sending message: {Message}", message);
        innerService.SendMessage(message);
        logger.LogTrace("Message sent.");
    }
}

[QudiDecorator(Lifetime = Lifetime.Singleton, Order = 2)]
public class CensorshipMessageServiceDecorator(
    IMessageService innerService
) : IMessageService
{
    public void SendMessage(string message)
    {
        var censoredMessage = message.Replace("badword", "***");
        innerService.SendMessage(censoredMessage);
    }
}

// -------------------
[DITransient]
public class MessageService : IMessageService { /* ... */ }

[DITransient]
public class MessageAnotherService : IMessageService { /* ... */ }

public interface IMessageService
{
    void SendMessage(string message);
}
```

When you resolve `IMessageService`, the decorators will be applied in the order specified by the `Order` property.

To quickly create decorator classes, you can also use the `DecoratorHelper` utility.
You only need to `override` the methods you want to customize; other methods will be automatically generated to simply call the inner service.

```csharp
[QudiDecorator(Lifetime = Lifetime.Singleton)]
public class SampleDecorator(IManyFeatureService service) : DecoratorHelper<IManyFeatureService>(service)
{
    public override void FeatureA()
    {
        Console.WriteLine("Before FeatureA");
        service.FeatureA();
    }
}

public interface IManyFeatureService
{
    void FeatureA();
    void FeatureB(int val);
    void FeatureC(string msg);
    void FeatureD(params string[] items);
    // and more...
}

// ---------------------
// generated code
public abstract class DecoratorHelper<T> where T : IManyFeatureService
{
    protected readonly T _innerService;
    protected DecoratorHelper(T innerService)
    {
        _innerService = innerService;
    }
    public virtual void FeatureA() => _innerService.FeatureA();
    public virtual void FeatureB(int val) => _innerService.FeatureB(val);
    public virtual void FeatureC(string msg) => _innerService.FeatureC(msg);
    public virtual void FeatureD(params string[] items) => _innerService.FeatureD(items);
    // and more...
}
```

### Customize Registration
Are you a customization nerd? You can customize various registration settings using the `[Qudi]` attribute.

```csharp
// For example, you can add custom attributes like this:
[Qudi(
    // Lifetime is required parameter
    Lifetime = Lifetime.Singleton, // or "Singleton"
    // Trigger registration only in specific conditions.
    // if empty, always registered.
    When = [Condition.Development, Condition.Production],
    // It is automatically identified, but you can also specify it explicitly
    AsTypes = [typeof(IYourService), typeof(IYourOtherService)],
    // Make this class accessible from other projects?
    UsePublic = true,
    // You can use Keyed registrations.
    Key = null,
    // Are you concerned about the order of registration? (default is 0, high value means later registration)
    Order = 0,
    // Set true if you want to register as a decorator
    MarkAsDecorator = false
)]
public class YourClass : IYourService, IYourOtherService { /* ... */ }

// [DI*] is just a shorthand for the above [Qudi] attribute, so you can use it like this:
// [DISingleton(When = [Condition.Development], AsTypes = [typeof(IYourService)], ...)]
```

> [!NOTE]
> If you need to perform more complex tasks, it is recommended to register them manually as before.

### Use Collected Information Directly
You can also refer to the collected information only and register it manually to the DI container.

```csharp
using Qudi.Generated;
var registrations = QudiInternalRegistrations.FetchAll();
// registration info like this:
// {
//     Type = typeof(Altaria),
//     Lifetime = "Singleton",
//     When = new List<string> {  },
//     AsTypes = new List<Type> { typeof(IPokemon) },
//     // and so on...
// },
```

## Architecture
This library performs the following tasks internally.

### Collecting class information
First, the source generator scans classes annotated with attributes like `DISingleton` and `DITransient`. Based on the results, it generates code such as the following:

<details>
<summary>Generated Code (Qudi.Registrations.g.cs)</summary>

```csharp
#nullable enable
using System.Linq;

namespace Qudi.Generated
{
    // Here we use an easy-to-reference namespace for internal calls
    internal static partial class QudiInternalRegistrations
    {
        public static IReadOnlyList<TypeRegistrationInfo> FetchAll()
            => Qudi.Generated__D716A886.QudiRegistrations.WithDependencies(fromOther: false);
    }
}
namespace Qudi.Generated__D716A886
{
    // Here we use an auto-generated namespace so Qudi can automatically invoke registrations including dependencies
    public static partial class QudiRegistrations
    {
        public static IReadOnlyList<Qudi.TypeRegistrationInfo> WithDependencies(bool fromOther = false)
        {
            var list = new List<TypeRegistrationInfo>();
            // If there are dependencies, they will be added here.
            // e.g. list.AddRange(Qudi.Generated__Deps1.QudiRegistrations.WithDependencies(fromOther: true));
            list.AddRange(Self(fromOther: fromOther));
            return list;
        }
        
        public static IReadOnlyList<TypeRegistrationInfo> Self(bool fromOther = false)
        {
            return Original.Where(t => t.UsePublic || !fromOther).ToList();
        }
        
        private static readonly IReadOnlyList<TypeRegistrationInfo> Original = new List<TypeRegistrationInfo>
        {
            new Qudi.TypeRegistrationInfo
            {
                Type = typeof(Altaria),
                Lifetime = "Singleton",
                When = new List<string> {  },
                AsTypes = new List<Type> { typeof(IPokemon) },
                UsePublic = true,
                Key = null,
                Order = 0,
                MarkAsDecorator = false,
                AssemblyName = "Qudi.Example.Readme"
            },
            new Qudi.TypeRegistrationInfo
            {
                Type = typeof(Abomasnow),
                Lifetime = "Transient",
                When = new List<string> {  },
                AsTypes = new List<Type> { typeof(IPokemon) },
                UsePublic = true,
                Key = null,
                Order = 0,
                MarkAsDecorator = false,
                AssemblyName = "Qudi.Example.Readme"
            },
        };
    }
}
```

</details>


As shown, information about annotated classes is collected as `TypeRegistrationInfo`. If dependencies exist, those are included automatically. Because this information is DI-container-agnostic, it can be used to support multiple DI containers.

### Invoking registrations for each container
Next, container-specific `AddQudiServices` extension methods are generated. For example, if Qudi is referenced, an extension for `Microsoft.Extensions.DependencyInjection` is generated:

<details>
<summary>Generated Code (Qudi.AddServices.g.cs)</summary>

```csharp
namespace Qudi;

internal static partial class QudiAddServiceExtensions
{
    public static IServiceCollection AddQudiServices(
        this IServiceCollection services,
        Action<QudiConfiguration>? configuration = null
    )
    {
        // Apply user configuration
        var config = new QudiConfiguration();
        configuration?.Invoke(config);
        // Create options to pass to registration handlers
        var options = new QudiAddServicesOptions
        {
            SelfAssemblyName = "Qudi.Example.Readme"
        };
        // Fetch registration information
        var types = Generated.QudiInternalRegistrations.FetchAll();
        // Call the registration handler for Microsoft.Extensions.DependencyInjection
        Qudi.QudiAddServiceForMicrosoftExtensionsDependencyInjection.AddQudiServices(services, types, config, options);
        return services;
    }
}
```

</details>

## Development Guides
### Testing
To run tests, simply execute the following command in the root directory:

```bash
# run normal tests
dotnet test
# run AOT tests ( e.g. Windows )
dotnet publish tests/Qudi.Tests/Qudi.Tests.csproj -o ./publish -f net10.0 -r win-x64 && publish\Qudi.Tests.exe 
```
