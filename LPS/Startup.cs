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

namespace LPS
{
    static class Startup
    {
        public static async Task ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((configBuilder) =>
                {
                    string projectDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Directory.GetCurrentDirectory())));
                    configBuilder.SetBasePath(projectDirectory)
                    .AddEnvironmentVariables();
                    configBuilder.AddJsonFile(@"lpsSettings.json", optional: false, reloadOnChange: false);

                })
                .ConfigureLPSFileLogger()
                .ConfigureServices((context, services) =>
                {
                    //Dependency Injection goes Here
                    services.AddHostedService(p => p.ResolveWith<Bootstrapper>(new { args = args }));
                   // services.AddSingleton<ILPSLogger, FileLogger>();
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
                .Build();
            await host.RunAsync();
        }


    }
}
