using LPS.Domain;
using LPS.Domain.Common;
using LPS.Infrastructure.Client;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Watchdog;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSValidators;
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
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if (lpsFileOptions == null)
                    lpsFileOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSFileLoggerConfiguration").Get<LPSFileLoggerOptions>();
                FileLogger fileLogger;
                bool isDefaultConfigurationsApplied = false;

                if (lpsFileOptions == null)
                {
                    fileLogger = FileLogger.GetDefaultInstance();
                    isDefaultConfigurationsApplied = true;
                    fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSFileLoggerConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                }
                else
                {
                    var loggerValidator = new LPSFileLoggerValidator();
                    var validationResults = loggerValidator.Validate(lpsFileOptions);
                    if (!validationResults.IsValid)
                    {
                        fileLogger = FileLogger.GetDefaultInstance();
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "Logger options are not valid. Default settings will be applied. You will need to fix the below errors.", LPSLoggingLevel.Warning);
                        validationResults.PrintValidationErrors();
                    }
                    else
                    {
                        // Create an instance of your custom logger implementation
                        fileLogger = new FileLogger(lpsFileOptions.LogFilePath, lpsFileOptions.LoggingLevel.Value,
                            lpsFileOptions.ConsoleLogingLevel.Value, lpsFileOptions.EnableConsoleLogging.Value,
                            lpsFileOptions.DisableConsoleErrorLogging.Value, lpsFileOptions.DisableFileLogging.Value);
                    }
                }

                if (isDefaultConfigurationsApplied)
                {
                    // Print Logger Options
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    string jsonString = JsonSerializer.Serialize(fileLogger);
                    Console.WriteLine($"Applied Logger Options: {jsonString}");
                    Console.ResetColor();
                }

                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILPSLogger>(fileLogger);

            });
            return hostBuilder;
        }

        public static IHostBuilder ConfigureLPSWatchdog(this IHostBuilder hostBuilder, LPSWatchdogOptions watchdogOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if (watchdogOptions == null)
                    watchdogOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSWatchdogConfiguration").Get<LPSWatchdogOptions>();
                LPSWatchdog watchdog;
                using var serviceProvider = services.BuildServiceProvider();
                var fileLogger = serviceProvider.GetRequiredService<ILPSLogger>();
                bool isDefaultConfigurationsApplied = false;
                if (watchdogOptions == null)
                {
                    watchdog = LPSWatchdog.GetDefaultInstance();
                    isDefaultConfigurationsApplied = true;
                    fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSWatchdogConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                }
                else
                {
                    var lpsWatchdogValidator = new LPSWatchdogValidator();
                    var validationResults = lpsWatchdogValidator.Validate(watchdogOptions);
                    if (!validationResults.IsValid)
                    {
                        watchdog = LPSWatchdog.GetDefaultInstance();
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "Watchdog options are not valid. Default settings will be applied. You will need to fix the below errors.", LPSLoggingLevel.Warning);
                        validationResults.PrintValidationErrors();
                    }
                    else
                    {
                        // Create an instance of your custom logger implementation
                        watchdog = new LPSWatchdog(watchdogOptions.MaxMemoryMB.Value,
                            watchdogOptions.MaxCPUPercentage.Value,
                            watchdogOptions.CoolDownMemoryMB.Value,
                            watchdogOptions.CoolDownCPUPercentage.Value,
                            watchdogOptions.MaxConcurrentConnectionsCountPerHostName.Value,
                            watchdogOptions.CoolDownConcurrentConnectionsCountPerHostName.Value,
                            watchdogOptions.CoolDownRetryTimeInSeconds.Value,
                            watchdogOptions.SuspensionMode.Value);
                    }
                }

                if (isDefaultConfigurationsApplied)
                {
                    // Print Logger Options
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    string jsonString = JsonSerializer.Serialize(watchdog);
                    Console.WriteLine($"Applied Watchdog Configuration: {jsonString}");
                    Console.ResetColor();
                }

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
                    lpsHttpClientOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSHttpClientConfiguration").Get<LPSHttpClientOptions>();

                LPSHttpClientConfiguration lpsHttpClientConfiguration;
                using var serviceProvider = services.BuildServiceProvider();
                var fileLogger = serviceProvider.GetRequiredService<ILPSLogger>();
                bool isDefaultConfigurationsApplied = false;
                if (lpsHttpClientOptions == null)
                {
                    lpsHttpClientConfiguration = LPSHttpClientConfiguration.GetDefaultInstance();
                    isDefaultConfigurationsApplied = true;
                    fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSHttpClientConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                }
                else
                {
                    LPSHttpClientValidator httpClientValidator = new LPSHttpClientValidator();
                    var validationResults = httpClientValidator.Validate(lpsHttpClientOptions);

                    if (!validationResults.IsValid)
                    {
                        lpsHttpClientConfiguration = LPSHttpClientConfiguration.GetDefaultInstance();
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "Http Client Options are not valid. Default settings will be applied. You will need to fix the below errors.", LPSLoggingLevel.Warning);
                        validationResults.PrintValidationErrors();

                    }
                    else
                    {
                        // Create an instance of your custom logger implementation
                        lpsHttpClientConfiguration = new LPSHttpClientConfiguration(TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionLifeTimeInSeconds.Value),
                           TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionIdleTimeoutInSeconds.Value), lpsHttpClientOptions.MaxConnectionsPerServer.Value,
                            TimeSpan.FromSeconds(lpsHttpClientOptions.ClientTimeoutInSeconds.Value));
                    }
                }


                if (isDefaultConfigurationsApplied)
                {
                    // Print Logger Options
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    string jsonString = JsonSerializer.Serialize(lpsHttpClientConfiguration);
                    Console.WriteLine($"Applied LPS Http Client Configuration: {jsonString}");
                    Console.ResetColor();
                }

                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILPSClientConfiguration<LPSHttpRequestProfile>>(lpsHttpClientConfiguration);

            });
            return hostBuilder;
        }

    }
}
