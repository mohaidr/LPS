using LPS.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using LPS.Infrastructure.Logging;
using LPS.UI.Core;
using System.Threading.Tasks;
using LPS.DIExtensions;
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
                .ConfigureServices((context, services) =>
                {

                    //Dpendency Injection goes Here
                    services.AddHostedService(p => p.ResolveWith<Bootstrapper>(new { args = args}));
                    services.AddSingleton<IFileLogger, FileLogger>();

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