using Microsoft.Extensions.Hosting.Internal;
using Qudi.Example.Core;

namespace Qudi.Example.Worker
{
    public class Worker(
        IHostApplicationLifetime applicationLifetime,
        NotifyPokemonInfoService notifyPokemonInfo
    ) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // wait a bit before notifying
            await Task.Delay(500, stoppingToken);
            // execute
            notifyPokemonInfo.NotifyAll();
            applicationLifetime.StopApplication();
            await Task.CompletedTask;
        }
    }
}
