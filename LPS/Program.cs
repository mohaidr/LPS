using Spectre.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LPS.UI.Core.Host;
using LPS.UI.Core.Web;

namespace LPS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "ui", StringComparison.OrdinalIgnoreCase))
            {
                await WebUiCommand.RunAsync(args[1..]);
                return;
            }

            AnsiConsole.Write(new FigletText("Load -- Perform {} Stress ^ ").Centered().Color(Color.Green));
            //DI Services
            using var host = Startup.ConfigureServices(args);
            var cancellationToken = host.Services.GetRequiredService<CancellationTokenSource>();

            await host.StartAsync(cancellationToken.Token);
            await host.Services.GetRequiredService<IFinalizationDisplay>()
                .ShowUntilShutdownAsync(host.WaitForShutdownAsync(CancellationToken.None));
        }

    }
}
