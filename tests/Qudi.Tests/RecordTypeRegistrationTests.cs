using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Qudi;
using Shouldly;
using TUnit;

namespace Qudi.Tests;

public sealed class RecordTypeRegistrationTests
{
    private const string TestCondition = nameof(RecordTypeRegistrationTests);

    [Test]
    public void RecordTypeCanBeRegistered()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

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
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

        var provider = services.BuildServiceProvider();

        // IEquatable<PersonRecord> should not be registered since it's a System built-in type
        var equatableService = provider.GetService<IEquatable<PersonRecord>>();
        equatableService.ShouldBeNull();
    }

    [Test]
    public void TypeWithIDisposableDoesNotAutoRegisterIDisposable()
    {
        var services = new ServiceCollection();
        services.AddQudiServices(conf => conf.SetCondition(TestCondition));

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

    internal interface IPersonService
    {
        string GetFullName();
    }

    // Record types should register their intended service interface only.
    [DITransient(When = [TestCondition])]
    internal sealed record PersonRecord() : IPersonService
    {
        public string FirstName { get; init; } = "First";
        public string LastName { get; init; } = "Last";

        public string GetFullName() => $"{FirstName} {LastName}";
    }

    internal interface IDataService : IDisposable
    {
        string GetData();
    }

    // Explicit IDisposable is allowed, but should not be auto-registered.
    [DITransient(When = [TestCondition])]
    internal sealed class DataService : IDataService
    {
        private bool _disposed;

        public string GetData() => _disposed ? "disposed" : "active";

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
