using System.Collections.Generic;
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
