# Qudi
[![NuGet Version](https://img.shields.io/nuget/v/Qudi?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Qudi/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Qudi/test.yaml?branch=main&label=Test&style=flat-square) 

**Qudi** (`/kʲɯːdiː/`, Quickly Dependency Injection) is an attribute-based **simple** dependency injection helper library.  
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

Alternatively, you can install `Qudi.Core`, `Qudi.Generator` and `Qudi.Container.*` packages separately.

```bash
# install Qudi.Core (common models)
dotnet add package Qudi.Core
# install Qudi.Generator (source generator)
dotnet add package Qudi.Generator
# install container-specific package (here, Microsoft.Extensions.DependencyInjection)
dotnet add package Qudi.Container.Microsoft
```

### Benefits
Compared to [Scrutor](https://github.com/khellang/Scrutor), the advantages of this library are as follows:

* **Explicit**: Registration is controlled using attributes. While this style depends on preference, I prefer explicit registration.
* **Conditional**: Conditional registration is possible via attribute parameters. Of course, Scrutor can do this too, but Qudi makes it easier to achieve.
* **No Assembly Scan**: No assembly scanning. It works in AOT environments and is very fast.

## Features
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

In this case, introduce `Qudi` (or `Qudi.Core`) to `MyApp.Core`.
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

> [!NOTE]
> If you want to switch processing dynamically according to conditions during runtime, consider using [Strategy Pattern](#strategy-pattern) or [Feature Flags](https://learn.microsoft.com/en-us/azure/azure-app-configuration/feature-management-dotnet-reference).

### Decorator Pattern
#### Overview
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

#### (TODO) Using Auto Generated Helper
To quickly implement decorators, by marking the target class as `partial`, an abstract helper class is automatically generated.

```csharp
// when use QudiDecoratorAttribute, marked partial and implements interface
[QudiDecorator(Lifetime = Lifetime.Singleton)]
public partial class SampleDecorator : IManyFeatureService
{
    // mark it partial and add constructor definition
    // required C# 14 or later for 'partial' constructor
    public partial SampleDecorator(
        IManyFeatureService innerService,
        ILogger<SampleDecorator> logger    
    );

    // Only override the methods you want to customize
    public override void FeatureA()
    {
        logger.LogTrace("Before FeatureA");
        base.FeatureA();
        logger.LogTrace("After FeatureA");
    }
    // For other methods, code is automatically generated to simply call innerService.
}

public interface IManyFeatureService
{
    void FeatureA();
    void FeatureB(int val);
    void FeatureC(string msg);
    Task FeatureD(params string[] items);
    // and more...
}
```

<details>
<summary>Generated Code Snippets</summary>

```csharp
partial class SampleDecorator : DecoratorHelper_IManyFeatureService
{
    private readonly ILogger<SampleDecorator> logger;
    public partial SampleDecorator(IManyFeatureService innerService, ILogger<SampleDecorator> logger)
        : base(innerService)
    {
        this.logger = logger;
    }
}

public abstract class DecoratorHelper_IManyFeatureService : IManyFeatureService
{
    protected readonly IManyFeatureService innerService;
    protected DecoratorHelper_IManyFeatureService(IManyFeatureService innerService)
    {
        this.innerService = innerService;
    }

    public virtual void FeatureA() => innerService.FeatureA();
    public virtual void FeatureB(int val) => innerService.FeatureB(val);
    public virtual void FeatureC(string msg) => innerService.FeatureC(msg);
    public virtual Task FeatureD(params string[] items) => innerService.FeatureD(items);
    // and more...
}
```

</details>

#### (TODO) Using Intercept
In addition to overriding individual methods, you can also use the `Intercept` method to perform operations for all method calls at once.
This is useful for logging, performance measurement, and other cross-cutting concerns (AOP-like behavior).

```csharp
[QudiDecorator(Lifetime = Lifetime.Singleton)]
public partial class SampleInterceptor : IManyFeatureService
{
    public partial SampleInterceptor(IManyFeatureService innerService);

    // add it
    protected override IEnumerable<bool> Intercept(string methodName, object?[] args)
    {
        // before
        var timer = new System.Diagnostics.Stopwatch();
        Console.WriteLine("Timer started...");
        timer.Start();
        yield return true; // if cancel execution, yield return false;
        // after
        timer.Stop();
        Console.WriteLine($"Execute time is {timer.ElapsedMilliseconds} ms");
    }

    // You can still override specific methods if needed
    public override async Task FeatureA()
    {
        Console.WriteLine("Before FeatureA");
        await base.FeatureA(); // call base method to ensure Intercept is invoked
        Console.WriteLine("After FeatureA");
    }
    // and more...
}
```

<details>
<summary>Generated Code Snippets</summary>

```csharp
partial class SampleInterceptor : DecoratorHelper_IManyFeatureService
{
    public partial SampleInterceptor(IManyFeatureService innerService)
        : base(innerService)
    {
    }
}

public abstract class DecoratorHelper_IManyFeatureService : IManyFeatureService
{
    protected readonly IManyFeatureService innerService;
    protected DecoratorHelper_IManyFeatureService(IManyFeatureService innerService)
    {
        this.innerService = innerService;
    }

    protected abstract IEnumerable<bool> Intercept(string methodName, object?[] args);

    public virtual async Task FeatureA()
    {
        var enumerator = Intercept(nameof(FeatureA), Array.Empty<object?>());
        // call hook before execution
        if (enumerator.MoveNext() && enumerator.Current)
        {
            // call the inner service
            await innerService.FeatureA();
            // call hook after execution
            enumerator.MoveNext();
        }
    }
    // and more...
}
```

</details>

### (TODO) Strategy Pattern
#### Overview
This is similar to the Decorator pattern, but switches services in a 1-to-many relationship instead of 1-to-1.
For example, consider a case where you want to switch between multiple implementations of a message service based on conditions.

```csharp
[QudiStrategy(Lifetime = Lifetime.Singleton)]
public class SendMessageStrategy(IEnumerable<IMessageService> services, MyConfiguration config) : IMessageService
{
    public void SendMessage(string message)
    {
        foreach (var service in services)
        {
            if (config.ShouldUseService(service))
            {
                service.SendMessage(message);
            }
        }
    }
}
```

Once defined this way, you can send messages through `SendMessageStrategy` instead of individual `IMessageService` implementations.

```csharp
[DISingleton]
public class NotificationService(IMessageService messageService)
{
    public void Notify(string message)
    {
        // Here, SendMessageStrategy is invoked.
        messageService.SendMessage(message); 
    }
}
```

#### (TODO) Using Auto Generated Helper
Like Decorators, by marking the target class as `partial`, an abstract helper class for quickly implementing strategies is automatically generated.

```csharp
// when use QudiStrategyAttribute, marked partial and implements interface
// the abstract helper class is automatically generated to help you implement strategy pattern.
[QudiStrategy(Lifetime = Lifetime.Singleton)]
public partial class MessageServiceStrategy : IMessageService
{
    // mark it partial and add constructor definition
    // required C# 14 or later for 'partial' constructor
    public partial MessageServiceStrategy(
        IEnumerable<IMessageService> services
    );

    protected override StrategyResult ShouldUseService(IMessageService service)
    {
        // Select which service to use based on conditions
        return new(){
            UseService = service is EmailMessageService, // For example, use only EmailMessageService
            Continue = true // Whether to continue checking other services
        };
    }
}
```

<details>
<summary>Generated Code Snippets</summary>

```csharp
partial class MessageServiceStrategy : StrategyHelper_IMessageService
{
    public partial MessageServiceStrategy(IEnumerable<IMessageService> services)
        : base(services)
    {
    }
}

public abstract class StrategyHelper_IMessageService : IMessageService
{
    protected readonly IEnumerable<IMessageService> services;
    protected StrategyHelper_IMessageService(IEnumerable<IMessageService> services)
    {
        this.services = services;
    }

    protected abstract StrategyResult ShouldUseService(IMessageService service);

    // For each method and property, code is generated to determine which service to use and invoke it.
    public virtual void SendMessage(string message)
    {
        foreach (var service in services)
        {
            var result = ShouldUseService(service);
            if (result.UseService)
            {
                service.SendMessage(message);
            }
            if (!result.Continue)
            {
                break;
            }
        }
    }
}
```

</details>

You can also combine it with Decorators.

```csharp
[QudiDecorator(Lifetime = Lifetime.Singleton)]
public partial class LoggingStrategyDecorator : IMessageService
{
    public partial LoggingStrategyDecorator(
        IMessageService innerService,
        ILogger<LoggingStrategyDecorator> logger
    );

    public override void SendMessage(string message)
    {
        logger.LogTrace("Sending message: {Message}", message);
        base.SendMessage(message);
    }
}

[QudiStrategy(Lifetime = Lifetime.Singleton)]
public partial class SendMessageStrategy : IMessageService
{
    public partial SendMessageStrategy(
        IEnumerable<IMessageService> services
    );

    protected override StrategyResult ShouldUseService(IMessageService service)
    {
        // ...
    }
}

// call chain is:
// IMessageService
// -> LoggingStrategyDecorator -> SendMessageStrategy
// -> ( individual IMessageService implementations )
```

When Order is the same, Decorators are applied first, followed by Strategies.


### (TODO) Visualize Missing Registrations
When registrations are missing for interfaces in your project, a visual runtime error like the following is output:

```
TODO
```

> [!NOTE]
> Missing registrations are detected only for interface types included in project dependencies.
> This is a limitation of Qudi's scanning approach, but it should be sufficient for most cases.

<details>
<summary>Why is this not an analyzer error?</summary>

Source Generators cannot directly reference code from dependent projects.
Therefore, we cannot accurately identify missing registrations on the dependency side, so we notify them as runtime errors instead.

</details>

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
    MarkAsDecorator = false,
    // Set true if you want to register as a strategy
    MarkAsStrategy = false
)]
public class YourClass : IYourService, IYourOtherService { /* ... */ }

// [DI*] is just a shorthand for the above [Qudi] attribute, so you can use it like this:
// [DISingleton(When = [Condition.Development], AsTypes = [typeof(IYourService)], ...)]
```

> [!TIP]
> If you need to perform more complex tasks, it is recommended to register them manually.

### Filtering Registrations
You can filter which registrations to apply by specifying options in the `AddQudiServices` method.

```csharp
services.AddQudiServices(conf => {
    conf.AddFilter(reg => {
        // e.g. filter by namespace
        return reg.Namespace.Contains("MyApp.Services");
    });
});
```

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
            new global::Qudi.TypeRegistrationInfo
            {
                Type = typeof(global::Altaria),
                Lifetime = "Singleton",
                When = new global::System.Collections.Generic.List<string> {  },
                AsTypes = new global::System.Collections.Generic.List<global::System.Type> { typeof(global::IPokemon) },
                UsePublic = true,
                Key = null,
                Order = 0,
                MarkAsDecorator = false,
                MarkAsStrategy = false,
                AssemblyName = "Qudi.Example.Readme",
                Namespace = "",
            },
            new global::Qudi.TypeRegistrationInfo
            {
                Type = typeof(global::Abomasnow),
                Lifetime = "Transient",
                When = new global::System.Collections.Generic.List<string> {  },
                AsTypes = new global::System.Collections.Generic.List<global::System.Type> { typeof(global::IPokemon) },
                UsePublic = true,
                Key = null,
                Order = 0,
                MarkAsDecorator = false,
                MarkAsStrategy = false,
                AssemblyName = "Qudi.Example.Readme",
                Namespace = "",
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

## Well-known Issues
- [ ] Assembly scan is performed every time, which is very slow for medium-sized projects or larger.
- [x] Record types are not registered.
- [ ] 