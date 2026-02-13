using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qudi;

namespace Qudi.Examples.DecoratorPattern;

public interface IMessageService
{
    void SendMessage(string message);
}

[DITransient]
public class MessageService : IMessageService
{
    public void SendMessage(string message)
    {
        Console.WriteLine($"📨 Sending: {message}");
    }
}

[QudiDecorator]
public class LoggingMessageServiceDecorator(
    IMessageService innerService,
    ILogger<LoggingMessageServiceDecorator> logger) : IMessageService
{
    public void SendMessage(string message)
    {
        logger.LogInformation("📋 Logging: About to send message: {Message}", message);
        innerService.SendMessage(message);
        logger.LogInformation("📋 Logging: Message sent successfully");
    }
}

[QudiDecorator(Order = 1)]
public class CensorshipMessageServiceDecorator(IMessageService innerService) : IMessageService
{
    public void SendMessage(string message)
    {
        var censoredMessage = message.Replace("badword", "***");
        if (message != censoredMessage)
        {
            Console.WriteLine("🚫 Censorship: Censored message");
        }
        innerService.SendMessage(censoredMessage);
    }
}

[DISingleton(Export = true)]
public class DecoratorPatternExecutor(IMessageService messageService) : ISampleExecutor
{
    public string Name => "Decorator Pattern";
    public string Description => "Add functionality with decorators using [QudiDecorator]";
    public string Namespace => typeof(DecoratorPatternExecutor).Namespace!;

    public void Execute()
    {
        Console.WriteLine("Sending a normal message:");
        messageService.SendMessage("Hello, World!");

        Console.WriteLine();
        Console.WriteLine("Sending a message with badword:");
        messageService.SendMessage("This contains badword in it");
    }
}
