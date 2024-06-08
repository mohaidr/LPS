using System;
using System.Threading.Tasks;
using System.Threading;
using Spectre.Console;
using LPS.UI.Core.Host;

namespace LPS
{
    class Program
    {
        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static bool _cancelRequested = false;
        static async Task Main(string[] args)
        {
            AnsiConsole.Write(new FigletText("Load -- Perform {} Stress ^ ").Centered().Color(Color.Green));
            var cancellationToken = _cts.Token;
            Console.CancelKeyPress += CancelKeyPressHandler;
            _ = WatchForCancellationAsync();
            //DI Services
            var host = Startup.ConfigureServices(args);
            await host.StartAsync(cancellationToken);
            if (LPSServer.IsRunning)
            {
                await LPSServer.ShutdownServerAsync();
            }
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                e.Cancel = true; // Prevent default process termination.
                AnsiConsole.MarkupLine("[yellow]Graceful shutdown requested (Ctrl+C/Break).[/]");
                RequestCancellation(); // Cancel the CancellationTokenSource.
            }
        }

        private static async Task WatchForCancellationAsync()
        {
            while (!_cancelRequested)
            {
                if (Console.KeyAvailable) // Check for the Escape key
                {
                    var keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        AnsiConsole.MarkupLine("[yellow]Graceful shutdown requested (Escape).[/]");
                        RequestCancellation(); // Cancel the CancellationTokenSource.
                        break; // Exit the loop
                    }
                }
                await Task.Delay(1000); // Poll every second
            }
        }

        private static void RequestCancellation()
        {
            if (!_cancelRequested)
            {
                _cancelRequested = true;
                AnsiConsole.MarkupLine("[yellow]Gracefully shutting down the LPS local server[/]");
                _cts.Cancel();
            }
        }
    }
}
