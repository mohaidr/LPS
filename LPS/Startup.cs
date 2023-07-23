using LPS.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using LPS.UI.Core;
using System.Threading.Tasks;
using LPS.DIExtensions;
using Microsoft.Extensions.Logging;
using LPS.Infrastructure.Logger;
using LPS.Domain;
using LPS.Infrastructure.Client;
using System;
using System.Threading;

namespace LPS
{
    static class Startup
    {
        public static async Task ConfigureServices(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += CancelKeyPressHandler;
            var cancellationToken = cts.Token;
            _= WatchForCancellationAsync(cts);
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((configBuilder) =>
                {
                    configBuilder.SetBasePath(Directory.GetCurrentDirectory())
                    .AddEnvironmentVariables();
                    configBuilder.AddJsonFile(@"config/lpsSettings.json", optional: false, reloadOnChange: false);

                })
                .ConfigureLPSFileLogger()
                .ConfigureServices((context, services) =>
                {
                    //Dependency Injection goes Here
                    services.AddHostedService(p => p.ResolveWith<LPSHostedService>(new { args = args }));
                    services.AddTransient<ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>>, LPSHttpClientManager>();
                    services.AddTransient<ILPSClientService<LPSHttpRequest>, LPSHttpClientService>();
                    services.AddTransient<ILPSClientConfiguration<LPSHttpRequest>, LPSHttpClientConfiguration>();
                    services.AddTransient<IRuntimeOperationIdProvider, RuntimeOperationIdProvider>();

                    if (context.HostingEnvironment.IsProduction())
                    {
                        //add production dependencies
                    }
                    else
                    {
                        // add development dependencies
                    }
                })
                .UseConsoleLifetime(options => options.SuppressStatusMessages = true)
                .Build();
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
                        _cancelRequested= true;
                    }
                }
                await Task.Delay(1000);
            }
            if(!_cancelRequested)
                cts.Cancel();
        }
    }
}
