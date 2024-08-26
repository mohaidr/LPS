using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using LPS.Infrastructure.Logger;
using LPS.Domain;
using LPS.UI.Common.Extensions;
using LPS.UI.Common;
using LPS.UI.Common.Options;
using Newtonsoft.Json;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring;
using LPS.UI.Core.Host;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Command;
using System.CommandLine;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.LPSClients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using LPS.UI.Core;
using Dashboard.Common;


namespace LPS
{
    static class Startup
    {
        public static IHost ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                    .UseSetting("http_port", GlobalSettings.Port.ToString())
                    .UseStartup<LPS.Dashboard.Startup>();
                    webBuilder.UseStaticWebAssets();
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.ListenAnyIP(GlobalSettings.Port);
                    });
                })
                .ConfigureAppConfiguration((configBuilder) =>
                {
                    configBuilder.AddEnvironmentVariables();
                    string lpsAppSettings = AppConstants.AppSettingsFileLocation;
                    if (!File.Exists(lpsAppSettings))
                    {
                        // If the file doesn't exist, create it with default settings
                        CreateDefaultAppSettings(lpsAppSettings);
                    }
                    configBuilder.AddJsonFile(lpsAppSettings, optional: false, reloadOnChange: false);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.ConfigureWritable<FileLoggerOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSFileLoggerConfiguration"), AppConstants.AppSettingsFileLocation);
                    services.ConfigureWritable<WatchdogOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSWatchdogConfiguration"), AppConstants.AppSettingsFileLocation);
                    services.ConfigureWritable<HttpClientOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSHttpClientConfiguration"), AppConstants.AppSettingsFileLocation);
                    services.AddSingleton<AppSettingsWritableOptions>();
                    services.AddSingleton<CancellationTokenSource>();
                    services.AddSingleton<ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun>, HttpRunCommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun>>();
                    services.AddSingleton<IClientManager<HttpRequestProfile, Domain.HttpResponse, IClientService<HttpRequestProfile, Domain.HttpResponse>>, HttpClientManager>();
                    services.AddSingleton<IClientService<HttpRequestProfile, Domain.HttpResponse>, HttpClientService>();
                    services.AddSingleton<IRuntimeOperationIdProvider, RuntimeOperationIdProvider>();
                    services.AddSingleton<IMetricsDataMonitor, MetricsDataMonitor>();
                    services.AddHostedService(p => p.ResolveWith<HostedService>(new { args }));
                    if (hostContext.HostingEnvironment.IsProduction())
                    {
                        //add production dependencies
                    }
                    else
                    {
                        // add development dependencies
                    }
                })
                .ConfigureLPSFileLogger()
                .ConfigureLPSWatchdog()
                .ConfigureLPSHttpClient()
                .UseConsoleLifetime(options => options.SuppressStatusMessages = true)
               .Build();

            return host;
        }

        private static void CreateDefaultAppSettings(string filePath)
        {
            // Get the directory path
            string directoryPath = Path.GetDirectoryName(filePath);

            // Check if the directory exists; if not, create it
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // You can customize this method to create the default settings
            // For example, you can create a basic JSON structure with default values
            var defaultSettings = new
            {
                LPSAppSettings = new AppSettings()
            };

            // Serialize the default settings object to JSON
            string jsonContent = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);

            // Write the JSON content to the specified file path
            File.WriteAllText(filePath, jsonContent);
        }
    }
}
