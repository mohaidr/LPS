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
using System.Reflection;


namespace LPS
{
    static class Startup
    {
        public static IHost ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    // Set the content root to the directory containing the executable
                    #pragma warning disable CS8602 // Dereference of a possibly null reference.
                    var contentRoot = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    ArgumentNullException.ThrowIfNull(contentRoot);
                    webBuilder.UseContentRoot(contentRoot);
                    // Set the web root to the wwwroot folder within the content root
                    var webRoot = Path.Combine(contentRoot, "wwwroot");
                    webBuilder.UseWebRoot(webRoot);

                    webBuilder.UseSetting("http_port", GlobalSettings.Port.ToString())
                              .UseStartup<LPS.Dashboard.Startup>();

                    // Ensure static web assets are used correctly
                    webBuilder.UseStaticWebAssets();
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.ListenAnyIP(GlobalSettings.Port);
                        serverOptions.AllowSynchronousIO = false; // Option to force async operations
                    })
                    .UseShutdownTimeout(TimeSpan.FromSeconds(30)); // Set shutdown timeout if necessary

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
                    services.AddSingleton<IMonitoredRunRepository, MonitoredRunRepository>();
                    services.AddSingleton<IMetricsDataMonitor, MetricsDataMonitor>();
                    services.AddSingleton<IMetricsQueryService, MetricsQueryService>();
                    services.AddSingleton<ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun>, HttpRunCommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun>>();
                    services.AddSingleton<AppSettingsWritableOptions>();
                    services.AddSingleton<CancellationTokenSource>();
                    services.AddSingleton<IClientManager<HttpRequestProfile, Domain.HttpResponse, IClientService<HttpRequestProfile, Domain.HttpResponse>>, HttpClientManager>();
                    services.AddSingleton<IRuntimeOperationIdProvider, RuntimeOperationIdProvider>();
                    services.ConfigureWritable<FileLoggerOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSFileLoggerConfiguration"), AppConstants.AppSettingsFileLocation);
                    services.ConfigureWritable<WatchdogOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSWatchdogConfiguration"), AppConstants.AppSettingsFileLocation);
                    services.ConfigureWritable<HttpClientOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSHttpClientConfiguration"), AppConstants.AppSettingsFileLocation);
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
