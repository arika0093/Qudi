using Qudi.Example.Core;

namespace Qudi.Example.Worker;

[DISingleton]
internal class NotifyToLogger(
    NotifyPokemonInfoService notifyPokemonInfoService,
    ILogger<NotifyToLogger> logger
) : INotificationService
{
    public void Notify(string message)
    {
        logger.LogInformation("Notification received: {Message}", message);
    }
}
