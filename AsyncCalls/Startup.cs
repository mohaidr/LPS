using AsyncTest.Domain;
using AsyncTest.Domain.Common;
using AsyncTest.UI.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using Serilog;

namespace AsyncCalls
{
    class Startup
    {
        public static void ConfigureServices(string [] args)
        {
            var builder = new ConfigurationBuilder();
            BuildConfig(builder);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Build())
                .Enrich.FromLogContext().WriteTo.File("")
                .CreateLogger();
            Log.Logger.Information("Application Starting");

            var host = Host.CreateDefaultBuilder().ConfigureServices((context, services) =>
            {

                //Dpendency Injection goes Here
                services.AddTransient<ITestService<ICommand<HttpAsyncTest>, HttpAsyncTest>, TestService<ICommand<HttpAsyncTest>, HttpAsyncTest>>();
                services.AddSingleton(Log.Logger);

            })
            .UseSerilog()
            .Build();

             var svc = ActivatorUtilities.CreateInstance<ITestService<ICommand<HttpAsyncTest>, HttpAsyncTest>>(host.Services);
             ICommand<HttpAsyncTest> setupCommand = new HttpAsyncTest.SetupCommand();

            svc.Run(setupCommand);

        }

        static void BuildConfig(IConfigurationBuilder builder)
        {
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .AddEnvironmentVariables();
        }
    }
}
