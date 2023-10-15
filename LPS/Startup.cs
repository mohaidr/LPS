using LPS.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using LPS.UI.Core;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LPS.Infrastructure.Logger;
using LPS.Domain;
using LPS.Infrastructure.Client;
using System;
using System.Threading;
using LPS.UI.Common.Extensions;

namespace LPS
{
    static class Startup
    {
        public static IHost ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((configBuilder) =>
                {
                    configBuilder.SetBasePath(Directory.GetCurrentDirectory())
                    .AddEnvironmentVariables();
                    configBuilder.AddJsonFile(@"config/lpsSettings.json", optional: false, reloadOnChange: false);

                })
                .ConfigureLPSFileLogger()
                .ConfigureLPSResourceTracker()
                .ConfigureServices((context, services) =>
                {
                    //Dependency Injection goes Here
                    services.AddHostedService(p => p.ResolveWith<LPSHostedService>(new { args = args }));
                    services.AddSingleton<ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>>, LPSHttpClientManager>();
                    services.AddSingleton<ILPSClientService<LPSHttpRequest>, LPSHttpClientService>();
                    services.AddSingleton<ILPSClientConfiguration<LPSHttpRequest>, LPSHttpClientConfiguration>();
                    services.AddSingleton<IRuntimeOperationIdProvider, RuntimeOperationIdProvider>();

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
            return host;
        }
    }
}
