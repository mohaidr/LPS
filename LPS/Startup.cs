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

namespace LPS
{
    static class Startup
    {
        public static async Task ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((configBuilder) =>
                {
                    configBuilder.SetBasePath(Directory.GetCurrentDirectory())
                    .AddEnvironmentVariables();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole(options => options.DisableColors = true);
                    logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
                })
                .ConfigureServices((context, services) =>
                {
                    //Dependency Injection goes Here
                    services.AddHostedService(p => p.ResolveWith<Bootstrapper>(new { args = args }));
                    services.AddSingleton<ICustomLogger, FileLogger>();
                    services.AddTransient<ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>>, LPSHttpClientManager>();
                    services.AddTransient<ILPSClientService<LPSHttpRequest>, LPSHttpClientService>();
                    services.AddTransient<ILPSClientConfiguration<LPSHttpRequest>, LPSHttpClientConfiguration>();

                    if (context.HostingEnvironment.IsProduction())
                    {
                        //add production dependencies
                    }
                    else
                    {
                        // add development dependencies
                    }
                })
                .Build();

            await host.RunAsync();
        }
    }
}
