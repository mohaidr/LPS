using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.LPSClients;
using LPS.Infrastructure.Watchdog;
using LPS.UI.Common.Options;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Build.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using LPS.Infrastructure.Nodes;

namespace LPS.UI.Common.Extensions
{


    public static class HostingExtensions
    {
        public static IHostBuilder ConfigureLPSFileLogger(this IHostBuilder hostBuilder, FileLoggerOptions? lpsFileOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                lpsFileOptions ??= hostContext.Configuration.GetSection("LPSAppSettings:LPSFileLoggerConfiguration").Get<FileLoggerOptions>();

                services.AddSingleton<ILogger>(serviceProvider =>
                {
                    FileLogger fileLogger;
                    bool isDefaultConfigurationsApplied = false;

                    if (lpsFileOptions == null)
                    {
                        fileLogger = new FileLogger(new LoggingConfiguration(),
                         serviceProvider.GetRequiredService<IConsoleLogger>(),
                         serviceProvider.GetRequiredService<ILogFormatter>());
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSFileLoggerConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                    }
                    else
                    {
                        var loggerValidator = new FileLoggerValidator();
                        var validationResults = loggerValidator.Validate(lpsFileOptions);
                        if (!validationResults.IsValid)
                        {
                            fileLogger = new FileLogger(new LoggingConfiguration(),
                                serviceProvider.GetRequiredService<IConsoleLogger>(),
                                serviceProvider.GetRequiredService<ILogFormatter>());
                            isDefaultConfigurationsApplied = true;
                            fileLogger.Log("0000-0000-0000-0000", "Logger options are not valid. Default settings will be applied. You will need to fix the below errors.", LPSLoggingLevel.Warning);
                            validationResults.PrintValidationErrors();
                        }
                        else
                        {
                            var loggingConfig = new LoggingConfiguration
                            {
                                LogFilePath = lpsFileOptions.LogFilePath,
                                LoggingLevel = lpsFileOptions.LoggingLevel.Value,
                                ConsoleLoggingLevel = lpsFileOptions.ConsoleLogingLevel.Value,
                                EnableConsoleLogging = lpsFileOptions.EnableConsoleLogging.Value,
                                DisableConsoleErrorLogging = lpsFileOptions.DisableConsoleErrorLogging.Value,
                                DisableFileLogging = lpsFileOptions.DisableFileLogging.Value
                            };

                            // Initialize the logger with LoggingConfiguration and services like IConsoleLogger, ILogFormatter
                            fileLogger = new FileLogger(
                                loggingConfig,
                                serviceProvider.GetRequiredService<IConsoleLogger>(),
                                serviceProvider.GetRequiredService<ILogFormatter>()
                            );
                        }
                    }

                    if (isDefaultConfigurationsApplied)
                    {
                        string jsonString = JsonSerializer.Serialize(fileLogger);
                        AnsiConsole.WriteLine($"[Magenta]Applied Logger Options: {jsonString}[/]");
                    }

                    return fileLogger;
                });
            });

            return hostBuilder;
        }
        public static IHostBuilder ConfigureLPSHttpClient(this IHostBuilder hostBuilder, HttpClientOptions? lpsHttpClientOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                lpsHttpClientOptions ??= hostContext.Configuration.GetSection("LPSAppSettings:LPSHttpClientConfiguration").Get<HttpClientOptions>();

                services.AddSingleton<IClientConfiguration<HttpRequest>>(serviceProvider =>
                {
                    var fileLogger = serviceProvider.GetRequiredService<ILogger>();
                    HttpClientConfiguration lpsHttpClientConfiguration;
                    bool isDefaultConfigurationsApplied = false;

                    if (lpsHttpClientOptions == null)
                    {
                        lpsHttpClientConfiguration = HttpClientConfiguration.GetDefaultInstance();
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSHttpClientConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                    }
                    else
                    {
                        HttpClientValidator httpClientValidator = new();
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
                            lpsHttpClientConfiguration = new HttpClientConfiguration(
                                TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionLifeTimeInSeconds.Value),
                                TimeSpan.FromSeconds(lpsHttpClientOptions.PooledConnectionIdleTimeoutInSeconds.Value),
                                lpsHttpClientOptions.MaxConnectionsPerServer.Value,
                                TimeSpan.FromSeconds(lpsHttpClientOptions.ClientTimeoutInSeconds.Value));
                        }
                    }

                    if (isDefaultConfigurationsApplied)
                    {
                        string jsonString = JsonSerializer.Serialize(lpsHttpClientConfiguration);
                        AnsiConsole.MarkupLine($"[Magenta]Applied LPS Http Client Configuration: {jsonString}[/]");
                    }

                    return lpsHttpClientConfiguration;
                });
            });

            return hostBuilder;
        }
        public static IHostBuilder ConfigureLPSWatchdog(this IHostBuilder hostBuilder, WatchdogOptions? watchdogOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                watchdogOptions ??= hostContext.Configuration.GetSection("LPSAppSettings:LPSWatchdogConfiguration").Get<WatchdogOptions>();

                // Register ILogger, IMetricsQueryService, and IRuntimeOperationIdProvider as services to avoid building a new service provider
                services.AddSingleton<IWatchdog>(serviceProvider =>
                {
                    var fileLogger = serviceProvider.GetRequiredService<ILogger>();
                    var metricsQueryService = serviceProvider.GetRequiredService<IMetricsQueryService>();
                    var runtimeOperationIdProvider = serviceProvider.GetRequiredService<IRuntimeOperationIdProvider>();

                    Watchdog watchdog;
                    bool isDefaultConfigurationsApplied = false;

                    if (watchdogOptions == null)
                    {
                        watchdog = Watchdog.GetDefaultInstance(fileLogger, runtimeOperationIdProvider, metricsQueryService);
                        isDefaultConfigurationsApplied = true;
                        fileLogger.Log("0000-0000-0000-0000", "LPSAppSettings:LPSWatchdogConfiguration Section is missing from the lpsSettings.json file. Default settings will be applied.", LPSLoggingLevel.Warning);
                    }
                    else
                    {
                        var lpsWatchdogValidator = new WatchdogValidator();
                        var validationResults = lpsWatchdogValidator.Validate(watchdogOptions);
                        if (!validationResults.IsValid)
                        {
                            watchdog = Watchdog.GetDefaultInstance(fileLogger, runtimeOperationIdProvider, metricsQueryService);
                            isDefaultConfigurationsApplied = true;
                            fileLogger.Log("0000-0000-0000-0000", "Watchdog options are not valid. Default settings will be applied. You will need to fix the below errors.", LPSLoggingLevel.Warning);
                            validationResults.PrintValidationErrors();
                        }
                        else
                        {
                            watchdog = new Watchdog(
                                watchdogOptions.MaxMemoryMB.Value,
                                watchdogOptions.MaxCPUPercentage.Value,
                                watchdogOptions.CoolDownMemoryMB.Value,
                                watchdogOptions.CoolDownCPUPercentage.Value,
                                watchdogOptions.MaxConcurrentConnectionsCountPerHostName.Value,
                                watchdogOptions.CoolDownConcurrentConnectionsCountPerHostName.Value,
                                watchdogOptions.CoolDownRetryTimeInSeconds.Value,
                                watchdogOptions.MaxCoolingPeriod.Value,
                                watchdogOptions.ResumeCoolingAfter.Value,
                                watchdogOptions.SuspensionMode.Value,
                                fileLogger,
                                runtimeOperationIdProvider,
                                metricsQueryService);
                        }
                    }

                    if (isDefaultConfigurationsApplied)
                    {
                        string jsonString = JsonSerializer.Serialize(watchdog);
                        AnsiConsole.MarkupLine($"[Magenta]Applied Watchdog Configuration: {jsonString}[/]");
                    }

                    return watchdog;
                });
            });

            return hostBuilder;
        }
        public static IHostBuilder ConfigureLPSCluster(this IHostBuilder hostBuilder, ClusterConfiguration? lpsClusterOptions = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                lpsClusterOptions ??= hostContext.Configuration.GetSection("LPSAppSettings:LPSClusterConfiguration").Get<ClusterConfiguration>();

                services.AddSingleton<IClusterConfiguration>(serviceProvider =>
                {
                    var logger = serviceProvider.GetRequiredService<ILogger>();
                    ClusterConfiguration clusterConfiguration;
                    bool isDefaultConfigurationsApplied = false;

                    if (lpsClusterOptions == null)
                    {
                        clusterConfiguration = new ClusterConfiguration
                        {
                            MasterNodeIP = "127.0.0.1",
                            WorkerRegistrationPort = 9009,
                            ExpectedNumberOfWorkers = 1
                        };
                        isDefaultConfigurationsApplied = true;
                        logger.Log("0000-0000-0000-0000", "LPSClusterConfiguration Section is missing from the settings file. Default settings will be applied.", LPSLoggingLevel.Warning);
                    }
                    else
                    {
                        var clusterValidator = new ClusteredConfigurationValidator();
                        var validationResults = clusterValidator.Validate(lpsClusterOptions);

                        if (!validationResults.IsValid)
                        {
                            clusterConfiguration = new ClusterConfiguration
                            {
                                MasterNodeIP = "127.0.0.1",
                                WorkerRegistrationPort = 9009,
                                ExpectedNumberOfWorkers = 1
                            };
                            isDefaultConfigurationsApplied = true;
                            logger.Log("0000-0000-0000-0000", "Cluster configuration options are not valid. Default settings will be applied.", LPSLoggingLevel.Warning);
                            validationResults.PrintValidationErrors();
                        }
                        else
                        {
                            clusterConfiguration = lpsClusterOptions;
                        }
                    }

                    if (isDefaultConfigurationsApplied)
                    {
                        string jsonString = JsonSerializer.Serialize(clusterConfiguration);
                        AnsiConsole.MarkupLine($"[Magenta]Applied LPS Cluster Configuration: {jsonString}[/]");
                    }

                    return clusterConfiguration;
                });
            });

            return hostBuilder;
        }
    }
}
