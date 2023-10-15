using LPS.Domain.Common;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.ResourceUsageTracker;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

       public static IHostBuilder ConfigureLPSFileLogger(this IHostBuilder hostBuilder, LPSFileLoggerConfiguration lpsFileConfig = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if (lpsFileConfig == null)
                    lpsFileConfig = hostContext.Configuration.GetSection("LPSFileLoggerConfiguration").Get<LPSFileLoggerConfiguration>();

                // Create an instance of your custom logger implementation
                var fileLogger = new FileLogger(lpsFileConfig.LogFilePath, lpsFileConfig.LoggingLevel, 
                    lpsFileConfig.ConsoleLogingLevel, lpsFileConfig.EnableConsoleLogging,
                    lpsFileConfig.EnableConsoleErrorLogging, lpsFileConfig.DisableFileLogging);

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

       public static IHostBuilder ConfigureLPSResourceTracker(this IHostBuilder hostBuilder, LPSResourceTrackerConfiguration usageTrackerConfiguration = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if (usageTrackerConfiguration == null)
                    usageTrackerConfiguration = hostContext.Configuration.GetSection("LPSResourceTrackerConfiguration").Get<LPSResourceTrackerConfiguration>();

                // Create an instance of your custom logger implementation
                var resourceUsageTracker = new LPSResourceTracker(usageTrackerConfiguration.MaxMemoryMB, 
                    usageTrackerConfiguration.MaxCPUPercentage,
                    usageTrackerConfiguration.CoolDownMemoryMB, 
                    usageTrackerConfiguration.CoolDownCPUPercentage,
                    usageTrackerConfiguration.CoolDownRetryTimeInSeconds, 
                    usageTrackerConfiguration.SuspensionMode);

                // Print Logger Options
                Console.ForegroundColor = ConsoleColor.Magenta;
                string jsonString = JsonSerializer.Serialize(resourceUsageTracker);
                Console.WriteLine($"Usage Tracker Configuration: {jsonString}");
                Console.ResetColor();


                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILPSResourceTracker>(resourceUsageTracker);

            });
            return hostBuilder;
        }

    }
}
