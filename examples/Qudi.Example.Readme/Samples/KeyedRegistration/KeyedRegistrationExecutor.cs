using Microsoft.Extensions.DependencyInjection;
using Qudi;

namespace Qudi.Examples.KeyedRegistration;

public interface INotificationService
{
    void Notify(string message);
}

[DITransient(Key = "email")]
public class EmailNotificationService : INotificationService
{
    public void Notify(string message)
    {
        Console.WriteLine($"📧 Email: {message}");
    }
}

[DITransient(Key = "sms")]
public class SmsNotificationService : INotificationService
{
    public void Notify(string message)
    {
        Console.WriteLine($"📱 SMS: {message}");
    }
}

[DITransient(Key = "push")]
public class PushNotificationService : INotificationService
{
    public void Notify(string message)
    {
        Console.WriteLine($"🔔 Push: {message}");
    }
}

[DITransient(Export = true)]
public class KeyedRegistrationExecutor(
    [FromKeyedServices("email")] INotificationService emailService,
    [FromKeyedServices("sms")] INotificationService smsService,
    [FromKeyedServices("push")] INotificationService pushService
) : ISampleExecutor
{
    public string Name => "Keyed Registration";
    public string Description => "Register services with keys and resolve them by key";
    public string Namespace => typeof(KeyedRegistrationExecutor).Namespace!;

    public void Execute()
    {
        emailService.Notify("You have a new message!");
        smsService.Notify("Your verification code is 123456");
        pushService.Notify("App update available");
    }
}
