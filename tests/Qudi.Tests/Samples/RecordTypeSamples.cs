using System;
using Qudi;

namespace Qudi.Tests;

// Test that record types can be registered without including System built-in interfaces
// Record types automatically implement IEquatable<T>, which should be excluded from auto-registration

public interface IPersonService
{
    string GetFullName();
}

[DITransient]
public sealed record PersonRecord() : IPersonService
{
    public string FirstName { get; init; } = "First";
    public string LastName { get; init; } = "Last";

    public string GetFullName() => $"{FirstName} {LastName}";
}

public interface IDataService : IDisposable
{
    string GetData();
}

// Test that explicitly implementing IDisposable is allowed, but it should not be auto-registered
[DITransient]
public sealed class DataService : IDataService
{
    private bool _disposed;

    public string GetData() => _disposed ? "disposed" : "active";

    public void Dispose()
    {
        _disposed = true;
    }
}
