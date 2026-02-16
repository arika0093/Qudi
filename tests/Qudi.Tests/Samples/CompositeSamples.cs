using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Qudi;

namespace Qudi.Tests;

// Simple composite example
public interface INotificationService
{
    void Notify(string message);
}

[DITransient]
public sealed class EmailNotificationService : INotificationService
{
    public void Notify(string message)
    {
        CompositeNotificationService.Messages.Add($"Email: {message}");
    }
}

[DITransient]
public sealed class SmsNotificationService : INotificationService
{
    public void Notify(string message)
    {
        CompositeNotificationService.Messages.Add($"SMS: {message}");
    }
}

[QudiComposite]
public sealed class CompositeNotificationService(IEnumerable<INotificationService> innerServices)
    : INotificationService
{
    public static readonly List<string> Messages = new();

    public void Notify(string message)
    {
        foreach (var service in innerServices)
        {
            service.Notify(message);
        }
    }
}

// Partial class composite example
public interface IMessageService2
{
    void Send(string message);
}

[DITransient]
public sealed class MessageServiceA : IMessageService2
{
    public void Send(string message)
    {
        CompositeMessageService.Messages.Add($"ServiceA: {message}");
    }
}

[DITransient]
public sealed class MessageServiceB : IMessageService2
{
    public void Send(string message)
    {
        CompositeMessageService.Messages.Add($"ServiceB: {message}");
    }
}

[QudiComposite]
public sealed partial class CompositeMessageService(IEnumerable<IMessageService2> innerServices)
    : IMessageService2
{
    public static readonly List<string> Messages = new();
}

// Test result aggregation with bool return type
public interface IValidationService
{
    bool Validate(string input);
}

[DITransient]
public sealed class LengthValidator : IValidationService
{
    public bool Validate(string input) => input.Length > 0;
}

[DITransient]
public sealed class AlphaValidator : IValidationService
{
    public bool Validate(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;
        foreach (var c in input)
        {
            if (!char.IsLetter(c))
                return false;
        }
        return true;
    }
}

[QudiComposite]
public sealed partial class CompositeValidationService(
    IEnumerable<IValidationService> innerServices
) : IValidationService;

// Test result aggregation with IEnumerable return type
public interface IDataProvider
{
    IEnumerable<string> GetData();
}

[DITransient]
public sealed class DataProviderA : IDataProvider
{
    public IEnumerable<string> GetData() => new[] { "A1", "A2" };
}

[DITransient]
public sealed class DataProviderB : IDataProvider
{
    public IEnumerable<string> GetData() => new[] { "B1", "B2" };
}

[QudiComposite]
public sealed partial class CompositeDataProvider(IEnumerable<IDataProvider> innerServices)
    : IDataProvider;

// Test result aggregation with Task return type
public interface IAsyncService
{
    Task ProcessAsync(string input);
}

[DITransient]
public sealed class AsyncServiceA : IAsyncService
{
    public async Task ProcessAsync(string input)
    {
        await Task.Delay(10);
        CompositeAsyncService.ProcessedItems.Add($"A:{input}");
    }
}

[DITransient]
public sealed class AsyncServiceB : IAsyncService
{
    public async Task ProcessAsync(string input)
    {
        await Task.Delay(10);
        CompositeAsyncService.ProcessedItems.Add($"B:{input}");
    }
}

[QudiComposite]
public sealed partial class CompositeAsyncService(IEnumerable<IAsyncService> innerServices)
    : IAsyncService
{
    public static readonly ConcurrentBag<string> ProcessedItems = new();
}
