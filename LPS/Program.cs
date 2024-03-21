using System;
using System.Threading.Tasks;
using System.Threading;
using Spectre.Console;

namespace LPS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.ReadLine();
            AnsiConsole.Write(new FigletText("Load -- Perform {} Stress ^ ").Centered().Color(Color.Green));

            CancellationTokenSource cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            Console.CancelKeyPress += CancelKeyPressHandler;
            _ = WatchForCancellationAsync(cts);
            //DI Services
            var host = Startup.ConfigureServices(args);
            await host.StartAsync(cancellationToken);
        }
        static bool _cancelRequested = false;
        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlC || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                e.Cancel = true; // Set e.Cancel to true to cancel the default behavior (exit the application).

                // Perform any custom actions you want before the application exits.
                // For example, you can prompt the user to confirm the exit or save data.

                // In this example, we'll just set a flag to indicate that the cancel was requested.
                _cancelRequested = true;
            }
        }
        public static async Task WatchForCancellationAsync(CancellationTokenSource cts)
        {
            while (!_cancelRequested)
            {
                if (Console.KeyAvailable)// check for escape
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.Escape)
                    {
                        cts.Cancel();
                        _cancelRequested = true;
                    }
                }
                await Task.Delay(1000);
            }
            if (!_cancelRequested)
                cts.Cancel();
        }
    }
}
