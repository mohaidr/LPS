using Spectre.Console;
using Microsoft.Extensions.DependencyInjection;

namespace LPS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AnsiConsole.Write(new FigletText("Load -- Perform {} Stress ^ ").Centered().Color(Color.Green));
            //DI Services
            var host = Startup.ConfigureServices(args);
            var cancelltionToken = host.Services.GetRequiredService<CancellationTokenSource>();

           
            await host.StartAsync(cancelltionToken.Token);
            await host.StopAsync(cancelltionToken.Token);
        }

    }
}
