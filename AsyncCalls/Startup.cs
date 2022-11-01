using AsyncTest.Domain;
using AsyncTest.Domain.Common;
using AsyncTest.UI.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using AsyncTest.Infrastructure.Logging;

namespace AsyncCalls
{
    static class Startup
    {
        internal static IHost _host;
        public static void ConfigureServices(string [] args)
        {
            var builder = new ConfigurationBuilder();
            BuildConfig(builder);

            _host = Host.CreateDefaultBuilder().ConfigureServices((context, services) =>
            {
                //Dpendency Injection goes Here
                services.AddSingleton<IFileLogger, FileLogger>();
                services.AddTransient<ITestService<ICommand<HttpAsyncTest>, HttpAsyncTest>, TestService<ICommand<HttpAsyncTest>, HttpAsyncTest>>();
            })
            .Build();
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
