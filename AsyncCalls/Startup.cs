using AsyncTest.Domain;
using AsyncTest.Domain.Common;
using AsyncTest.UI.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using AsyncTest.Infrastructure.Logging;
using AsyncCalls.UI.Core;
using System.Threading.Tasks;
using AsyncCalls.DIExtensions;
namespace AsyncCalls
{
    static class Startup
    {
        public static async Task ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((configBuilder) =>
               {
                   configBuilder.SetBasePath(Directory.GetCurrentDirectory())
                 //  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  // .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                   .AddEnvironmentVariables();
               })
                .ConfigureServices((context, services) =>
                {

                    //Dpendency Injection goes Here
                    services.AddHostedService(p => p.ResolveWith<Bootstrapper>(new { args = args}));
                    services.AddSingleton<IFileLogger, FileLogger>();
                    services.AddTransient<ITestService<ICommand<HttpAsyncTest>, HttpAsyncTest>, TestService<ICommand<HttpAsyncTest>, HttpAsyncTest>>();

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
