using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.LPSClients;
using LPS.Infrastructure.Watchdog;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.UI.Build.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace LPS.UI.Common.Extensions
{
    public static class ServiceProviderExtension
    {
        public static T ResolveWith<T>(this IServiceProvider provider, params object[] parameters) where T : class =>
            ActivatorUtilities.CreateInstance<T>(provider, parameters);

    }

    public static class HostingHostBuilderExtensions
    {

        public static IHostBuilder ConfigureLPSFileLogger(this IHostBuilder hostBuilder, FileLoggerOptions? lpsFileOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if (lpsFileOptions == null)
                    lpsFileOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSFileLoggerConfiguration").Get<FileLoggerOptions>();
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
                    var loggerValidator = new FileLoggerValidator();
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
                    string jsonString = JsonSerializer.Serialize(fileLogger);
                    Console.WriteLine($"[Magenta]Applied Logger Options: {jsonString}[/]");
                }

                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILogger>(fileLogger);

            });
            return hostBuilder;
        }

        public static IHostBuilder ConfigureLPSWatchdog(this IHostBuilder hostBuilder, WatchdogOptions? watchdogOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                if (watchdogOptions == null)
                    watchdogOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSWatchdogConfiguration").Get<WatchdogOptions>();
                Watchdog watchdog;
                using var serviceProvider = services.BuildServiceProvider();
                var fileLogger = serviceProvider.GetRequiredService<ILogger>();
                bool isDefaultConfigurationsApplied = false;

                if (watchdogOptions == null)
                {
                    watchdog = Watchdog.GetDefaultInstance(serviceProvider.GetRequiredService<ILogger>(), serviceProvider.GetRequiredService<IRuntimeOperationIdProvider>());
                    isDefaultConfigurationsApplied = true;
                    fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSWatchdogConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                }
                else
                {
                    var lpsWatchdogValidator = new WatchdogValidator();
                    var validationResults = lpsWatchdogValidator.Validate(watchdogOptions);
                    if (!validationResults.IsValid)
                    {
                        watchdog = Watchdog.GetDefaultInstance(serviceProvider.GetRequiredService<ILogger>(), serviceProvider.GetRequiredService<IRuntimeOperationIdProvider>());
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "Watchdog options are not valid. Default settings will be applied. You will need to fix the below errors.", LPSLoggingLevel.Warning);
                        validationResults.PrintValidationErrors();
                    }
                    else
                    {
                        watchdog = new Watchdog(watchdogOptions.MaxMemoryMB.Value,
                            watchdogOptions.MaxCPUPercentage.Value,
                            watchdogOptions.CoolDownMemoryMB.Value,
                            watchdogOptions.CoolDownCPUPercentage.Value,
                            watchdogOptions.MaxConcurrentConnectionsCountPerHostName.Value,
                            watchdogOptions.CoolDownConcurrentConnectionsCountPerHostName.Value,
                            watchdogOptions.CoolDownRetryTimeInSeconds.Value,
                            watchdogOptions.MaxCoolingPeriod.Value,
                            watchdogOptions.ResumeCoolingAfter.Value,
                            watchdogOptions.SuspensionMode.Value,
                            serviceProvider.GetRequiredService<ILogger>(), 
                            serviceProvider.GetRequiredService<IRuntimeOperationIdProvider>());
                    }
                }

                if (isDefaultConfigurationsApplied)
                {
                    string jsonString = JsonSerializer.Serialize(watchdog);
                    AnsiConsole.MarkupLine($"[Magenta]Applied Watchdog Configuration: {jsonString}[/]");
                }

                services.AddSingleton<IWatchdog>(watchdog);

            });
            return hostBuilder;
        }

        public static IHostBuilder ConfigureLPSHttpClient(this IHostBuilder hostBuilder, HttpClientOptions? lpsHttpClientOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                if (lpsHttpClientOptions == null)
                    lpsHttpClientOptions = hostContext.Configuration.GetSection("LPSAppSettings:LPSHttpClientConfiguration").Get<HttpClientOptions>();

                HttpClientConfiguration lpsHttpClientConfiguration;
                using var serviceProvider = services.BuildServiceProvider();
                var fileLogger = serviceProvider.GetRequiredService<ILogger>();
                bool isDefaultConfigurationsApplied = false;
                if (lpsHttpClientOptions == null)
                {
                    lpsHttpClientConfiguration = HttpClientConfiguration.GetDefaultInstance();
                    isDefaultConfigurationsApplied = true;
                    fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSHttpClientConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                }
                else
                {
                    HttpClientValidator httpClientValidator = new HttpClientValidator();
                    var validationResults = httpClientValidator.Validate(lpsHttpClientOptions);

                    if (!validationResults.IsValid)
                    {
                        lpsHttpClientConfiguration = HttpClientConfiguration.GetDefaultInstance();
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "Http Client Options are not valid. Default settings will be applied. You will need to fix the below errors.", LPSLoggingLevel.Warning);
                        validationResults.PrintValidationErrors();

                    }
                    else
                    {
                        lpsHttpClientConfiguration = new HttpClientConfiguration(TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionLifeTimeInSeconds.Value),
                           TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionIdleTimeoutInSeconds.Value), lpsHttpClientOptions.MaxConnectionsPerServer.Value,
                            TimeSpan.FromSeconds(lpsHttpClientOptions.ClientTimeoutInSeconds.Value));
                    }
                }


                if (isDefaultConfigurationsApplied)
                {
                    string jsonString = JsonSerializer.Serialize(lpsHttpClientConfiguration);
                    AnsiConsole.MarkupLine($"[Magenta]Applied LPS Http Client Configuration: {jsonString}[/]");
                }

                services.AddSingleton<IClientConfiguration<HttpRequestProfile>>(lpsHttpClientConfiguration);

            });
            return hostBuilder;
        }

    }
}
