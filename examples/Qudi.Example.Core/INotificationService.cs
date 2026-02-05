namespace Qudi.Example.Core;

/// <summary>
/// This is an example that only defines the interface, with the implementation provided elsewhere.
/// </summary>
public interface INotificationService
{
    // Sends a notification with the given message.
    void Notify(string message);
}
