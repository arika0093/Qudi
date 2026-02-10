using System;
using System.Collections.Generic;
using System.Text;
using Qudi.Example.Core;

namespace Qudi.Example.Worker;

/// <summary>
/// This is an example of a decorator that adds a prefix to the notification message.
/// </summary>
/// <param name="decorated"></param>
[QudiDecorator(Lifetime = Lifetime.Singleton)]
internal class NotifyDecorator(INotificationService decorated) : INotificationService
{
    public void Notify(string message) => decorated.Notify($"MSG<{message}>");
}
