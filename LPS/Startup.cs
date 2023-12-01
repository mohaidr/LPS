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
using System.Reflection;
using LPS.UI.Common;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace LPS
{
    static class Startup
    {
        public static IHost ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((configBuilder) =>
                {
                    configBuilder.AddEnvironmentVariables();
                    string lpsAppSettings = LPSAppConstants.AppSettingsFileLocation;
                    if (!File.Exists(lpsAppSettings))
                    {
                        // If the file doesn't exist, create it with default settings
                        CreateDefaultAppSettings(lpsAppSettings);
                    }
                    configBuilder.AddJsonFile(lpsAppSettings, optional: false, reloadOnChange: false);
                })
                .ConfigureLPSFileLogger()
                .ConfigureLPSWatchdog()
                .ConfigureLPSHttpClient()
                .ConfigureServices((hostContext, services) =>
                {
                    services.ConfigureWritable<LPSFileLoggerOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSFileLoggerConfiguration"), LPSAppConstants.AppSettingsFileLocation);
                    services.ConfigureWritable<LPSWatchdogOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSWatchdogConfiguration"), LPSAppConstants.AppSettingsFileLocation);
                    services.ConfigureWritable<LPSHttpClientOptions>(hostContext.Configuration.GetSection("LPSAppSettings:LPSHttpClientConfiguration"), LPSAppConstants.AppSettingsFileLocation);
                    services.AddSingleton<LPSAppSettingsWritableOptions>();
                    services.AddHostedService(p => p.ResolveWith<LPSHostedService>(new { args }));
                    services.AddSingleton<ILPSClientManager<LPSHttpRequestProfile, ILPSClientService<LPSHttpRequestProfile>>, LPSHttpClientManager>();
                    services.AddSingleton<ILPSClientService<LPSHttpRequestProfile>, LPSHttpClientService>();
                    services.AddSingleton<IRuntimeOperationIdProvider, RuntimeOperationIdProvider>();
                    if (hostContext.HostingEnvironment.IsProduction())
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

        private static void CreateDefaultAppSettings(string filePath)
        {
            // You can customize this method to create the default settings
            // For example, you can create a basic JSON structure with default values
            var defaultSettings = new
            {
                LPSAppSettings = new LPSAppSettings()
            };

            // Serialize the default settings object to JSON
            string jsonContent = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);

            // Write the JSON content to the specified file path
            File.WriteAllText(filePath, jsonContent);
        }
    }
}
