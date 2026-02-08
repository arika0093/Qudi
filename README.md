# Qudi
[![NuGet Version](https://img.shields.io/nuget/v/Qudi?style=flat-square&logo=NuGet&color=0080CC)](https://www.nuget.org/packages/Qudi/) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/arika0093/Qudi/test.yaml?branch=main&label=Test&style=flat-square) 

**Qudi** (`/kʲɯːdiː/`, Quickly Dependency Injection) is an attribute-based dependency injection helper library.  
No assembly scan, AOT friendly.

## Quick Start
### First step
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
```

As you can see, just these two steps. 

1. Mark each class with attributes like `[DISingleton]`, `[DITransient]`, etc.
2. Call `IServiceCollection.AddQudiServices`.

When written like this, the following equivalent code is automatically generated and registered in the DI container:

```csharp
services.AddSingleton<Altaria>();
services.AddTransient<Abomasnow>();

services.AddSingleton<IPokemon, Altaria>(provider => provider.GetRequiredService<Altaria>());
services.AddTransient<IPokemon, Abomasnow>(provider => provider.GetRequiredService<Abomasnow>());
```

Want to know more about the internal behavior? See the [Architecture](#architecture) section.

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

In such cases, first introduce `Qudi` in each project.
you can create a `Directory.Build.props` file in the parent directory and set it up as follows to share the package reference.

```xml
<!-- in Directory.Build.props -->
<Project>
  <ItemGroup Label="Qudi Packages">
    <PackageReference Include="Qudi" Version="*" />
    <PackageReference Include="Qudi.Generator" Version="*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <Using Include="Qudi" />
  </ItemGroup>
</Project>
```

Next, mark the implementation class and the dependent class with Qudi attributes.

```csharp
// in MyApp.Windows
[DISingleton]
internal class SqlDataRepository : IDataRepository { /* ... */ }

// in MyApp.Windows
[DITransient]
internal class MyService(IDataRepository repository) { /* ... */ }
```

Then, just call `AddQudiServices` as usual :)

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
    conf.SetConditionFromHostEnvironment(builder.Environment);
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


### Customize Registration
Are you a customization nerd? No problem.  
we have plenty of options for you! (though it contradicts the first description :P )

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