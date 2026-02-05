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

### What happens here?
The process is very simple.
It scans all classes marked with attributes like `DISingleton` and `DITransient`, so this library generates the following code.

```csharp
internal static class QudiServiceCollectionExtensions
{
    public static IServiceCollection AddQudiServices(this IServiceCollection services)
    {
        // first, register concrete types
        services.AddSingleton<Altaria>();
        services.AddTransient<Abomasnow>();
        // then, register interfaces to concrete types.
        // (By registering like this, the same instance can be shared, which is often the desired behavior)
        services.AddSingleton<IPokemon, Altaria>(
            provider => provider.GetRequiredService<Altaria>()
        );
        services.AddTransient<IPokemon, Abomasnow>(
            provider => provider.GetRequiredService<Abomasnow>()
        );
        return services;
    }
}
```

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

### Customize Registration
You can customize the registration process by providing a delegate to the `TypeRegistration` method.

```csharp
services.AddQudiServices(conf => {
    // Middleware style customization
    conf.AddCustomRegistration(args => {
        var typeInfo = args.TypeInfo;
        // Here comes the information of the marked class, so feel free to cook it as you like
        Console.WriteLine($"""
            Type: {typeInfo.Type.FullName}
            Namespace: {typeInfo.Type.Namespace}
            Lifetime: {typeInfo.Lifetime}
            AsTypes: {string.Join(", ", typeInfo.AsTypes.Select(t => t.FullName))}
            UsePublic: {typeInfo.UsePublic}
            Key: {typeInfo.Key}
            Order: {typeInfo.Order}
            """);

        // Return false to let the subsequent process handle it.
        return false; 
    });
});
```

## Packages
This library is divided into several NuGet packages.

### Qudi
このパッケージは`MS.DI`用の拡張メソッドを提供します。


### Qudi.Core
This package contains only the attribute definitions and related constants.

### Qudi.Generator
This package provides a source generator that scans for classes marked with attributes and returns their information as an array.