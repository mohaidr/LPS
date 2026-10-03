using Spectre.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LPS.UI.Core.Host;
using LPS.UI.Core.Web;
using LPS.UI.Core.Distributed;
using LPS.Infrastructure.Distributed;

namespace LPS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && (args[0].Equals("worker", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("master", StringComparison.OrdinalIgnoreCase) && args.Any(argument =>
                    argument.Split('=', 2)[0] is "--listen" or "--workers" or "--settings" or "--masternodeisworker" or "-miw")))
            {
                await DistributedCommand.RunAsync(args);
                return;
            }
            if (args.Length > 0 && args[0] == "cluster-run" && ClusterRunSettings.Current == null)
            {
                Console.Error.WriteLine("cluster-run requires private distributed-run settings. Use lps worker or lps master --workers.");
                Environment.ExitCode = 1;
                return;
            }
            if (args.Length > 0 && string.Equals(args[0], "ui", StringComparison.OrdinalIgnoreCase))
            {
                await WebUiCommand.RunAsync(args[1..]);
                return;
            }

            if (ClusterRunSettings.Current == null)
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
