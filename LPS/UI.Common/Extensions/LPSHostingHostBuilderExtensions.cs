using LPS.Domain;
using LPS.Domain.Common;
using LPS.Infrastructure.Client;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Watchdog;
using LPS.UI.Common.Options;
using LPS.UI.Core.UI.Build.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace LPS.UI.Common.Extensions
{
    public static class LPSServiceProviderExtension
    {
        public static T ResolveWith<T>(this IServiceProvider provider, params object[] parameters) where T : class =>
            ActivatorUtilities.CreateInstance<T>(provider, parameters);

    }

    public static class LPSHostingHostBuilderExtensions
    {

        public static IHostBuilder ConfigureLPSFileLogger(this IHostBuilder hostBuilder, LPSFileLoggerOptions lpsFileOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                var loggerValidator = new LPSFileLoggerValidator();
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                FileLogger fileLogger;
                if (lpsFileOptions == null)
                    lpsFileOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSFileLoggerConfiguration").Get<LPSFileLoggerOptions>();

                var validationResults = loggerValidator.Validate(lpsFileOptions);

                if (!validationResults.IsValid)
                {
                    fileLogger = new FileLogger("lps-logs.log", LPSLoggingLevel.Verbos, LPSLoggingLevel.Information);
                    fileLogger.Log("0000-0000-0000-0000", "Logger options are not valid, the default settings were applies", LPSLoggingLevel.Warning);
                    validationResults.PrintValidationErrors();
                }
                else
                {
                    // Create an instance of your custom logger implementation
                    fileLogger = new FileLogger(lpsFileOptions.LogFilePath, lpsFileOptions.LoggingLevel,
                        lpsFileOptions.ConsoleLogingLevel, lpsFileOptions.EnableConsoleLogging,
                        lpsFileOptions.DisableConsoleErrorLogging, lpsFileOptions.DisableFileLogging);
                }

                // Print Logger Options
                Console.ForegroundColor = ConsoleColor.Magenta;
                string jsonString = JsonSerializer.Serialize(fileLogger);
                Console.WriteLine($"Logger Options: {jsonString}");
                Console.ResetColor();

                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILPSLogger>(fileLogger);

            });
            return hostBuilder;
        }

        public static IHostBuilder ConfigureLPSWatchdog(this IHostBuilder hostBuilder, LPSWatchDogOptions watchdogOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if (watchdogOptions == null)
                    watchdogOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSWatchdogConfiguration").Get<LPSWatchDogOptions>();

                // Create an instance of your custom logger implementation
                var watchdog = new LPSWatchdog(watchdogOptions.MaxMemoryMB,
                    watchdogOptions.MaxCPUPercentage,
                    watchdogOptions.CoolDownMemoryMB,
                    watchdogOptions.CoolDownCPUPercentage,
                    watchdogOptions.MaxConnectionsCountPerHostName,
                    watchdogOptions.CoolDownConnectionsCountPerHostName,
                    watchdogOptions.CoolDownRetryTimeInSeconds,
                    watchdogOptions.SuspensionMode);

                // Print Logger Options
                Console.ForegroundColor = ConsoleColor.Magenta;
                string jsonString = JsonSerializer.Serialize(watchdog);
                Console.WriteLine($"Usage Tracker Configuration: {jsonString}");
                Console.ResetColor();
                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILPSWatchdog>(watchdog);

            });
            return hostBuilder;
        }

        public static IHostBuilder ConfigureLPSHttpClient(this IHostBuilder hostBuilder, LPSHttpClientOptions lpsHttpClientOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if (lpsHttpClientOptions == null)
                {
                    lpsHttpClientOptions = hostContext.Configuration
                    .GetSection("LPSAppSettings:LPSHttpClientConfiguration")
                    .Get<LPSHttpClientOptions>();
                }

                // Create an instance of your custom logger implementation
                var lpsHttpClientConfiguration = new LPSHttpClientConfiguration(TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionLifeTimeInSeconds),
                   TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionIdleTimeoutInSeconds), lpsHttpClientOptions.MaxConnectionsPerServer,
                    TimeSpan.FromSeconds(lpsHttpClientOptions.ClientTimeoutInSeconds));

                // Print Logger Options
                Console.ForegroundColor = ConsoleColor.Magenta;
                string jsonString = JsonSerializer.Serialize(lpsHttpClientConfiguration);
                Console.WriteLine($"LPS Http Client Configuration: {jsonString}");
                Console.ResetColor();


                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILPSClientConfiguration<LPSHttpRequestProfile>>(lpsHttpClientConfiguration);

            });
            return hostBuilder;
        }

    }
}
