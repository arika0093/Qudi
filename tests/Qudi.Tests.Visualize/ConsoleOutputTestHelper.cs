using System;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Qudi.Tests.Visualize;

internal static class ConsoleOutputTestHelper
{
    private static readonly object ConsoleLock = new();

    internal static string CaptureConsoleOutput(
        Action<QudiConfigurationRootBuilder, QudiMicrosoftConfigurationBuilder> configure,
        Action<IServiceProvider> execute
    )
    {
        lock (ConsoleLock)
        {
            using var console = new TestConsole();
            console.EmitAnsiSequences = false;
            console.SupportsAnsi(false).Colors(ColorSystem.NoColors).Width(200).Height(200);

            var originalConsole = AnsiConsole.Console;
            AnsiConsole.Console = console;
            try
            {
                var services = new ServiceCollection();
                services.AddQudiServices(configure);
                using var provider = services.BuildServiceProvider();
                execute(provider);
            }
            finally
            {
                AnsiConsole.Console = originalConsole;
            }

            return console.Output;
        }
    }
}
