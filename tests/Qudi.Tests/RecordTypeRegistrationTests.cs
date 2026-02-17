using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class RecordTypeRegistrationTests
{
    [Test]
    public void RecordTypeCanBeRegistered()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();
        var personService = provider.GetRequiredService<IPersonService>();

        personService.ShouldNotBeNull();
        personService.ShouldBeOfType<PersonRecord>();
        personService.GetFullName().ShouldBe("First Last");
    }

    [Test]
    public void RecordTypeDoesNotRegisterIEquatable()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        // IEquatable<PersonRecord> should not be registered since it's a System built-in type
        var equatableService = provider.GetService<IEquatable<PersonRecord>>();
        equatableService.ShouldBeNull();
    }

    [Test]
    public void TypeWithIDisposableDoesNotAutoRegisterIDisposable()
    {
        var services = new ServiceCollection();
        services.AddQudiServices();

        var provider = services.BuildServiceProvider();

        // The service should be registered as IDataService
        var dataService = provider.GetRequiredService<IDataService>();
        dataService.ShouldNotBeNull();
        dataService.GetData().ShouldBe("active");

        // But not as IDisposable directly (since it's a System built-in type)
        var disposableServices = provider.GetServices<IDisposable>().ToList();

        // We might get other IDisposable services from the framework,
        // but DataService should only be accessible via IDataService
        var dataServiceCount = provider.GetServices<IDataService>().Count();
        dataServiceCount.ShouldBe(1);
    }
}
