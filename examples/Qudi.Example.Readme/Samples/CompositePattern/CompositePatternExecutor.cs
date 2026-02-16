using Microsoft.Extensions.DependencyInjection;
using Qudi;

namespace Qudi.Examples.CompositePattern;

public interface IMessageService
{
    void SendMessage(string message);
}

[DITransient]
public class EmailMessageService : IMessageService
{
    public void SendMessage(string message)
    {
        Console.WriteLine($"📧 Email: Sending '{message}' via email");
    }
}

[DITransient]
public class SmsMessageService : IMessageService
{
    public void SendMessage(string message)
    {
        Console.WriteLine($"📱 SMS: Sending '{message}' via SMS");
    }
}

[DITransient]
public class PushNotificationService : IMessageService
{
    public void SendMessage(string message)
    {
        Console.WriteLine($"🔔 Push: Sending '{message}' via push notification");
    }
}

// Composite service that combines multiple IMessageService implementations
[QudiComposite]
public class CompositeMessageService(IEnumerable<IMessageService> innerServices)
    : IMessageService
{
    // innerServices will automatically contain all registered IMessageService implementations.
    public void SendMessage(string message)
    {
        Console.WriteLine("📦 Composite: Broadcasting message to all channels...");
        foreach (var service in innerServices)
        {
            service.SendMessage(message);
        }
    }
}

[DITransient(Export = true)]
public class CompositePatternExecutor(IMessageService messageService) : ISampleExecutor
{
    public string Name => "Composite Pattern";
    public string Description => "Combine multiple services with [QudiComposite]";
    public string Namespace => typeof(CompositePatternExecutor).Namespace!;

    public void Execute()
    {
        Console.WriteLine("Using composite to send message through all channels:");
        messageService.SendMessage("Hello, World!");
    }
}
