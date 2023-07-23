using LPS.Domain.Common;
using LPS.Infrastructure.Logger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace LPS.DIExtensions
{
    public static class ServiceProviderExtensions
    {
        public static T ResolveWith<T>(this IServiceProvider provider, params object[] parameters) where T : class =>
            ActivatorUtilities.CreateInstance<T>(provider, parameters);

    }
    
    public static class CustomLoggingExtensions
    {
        public class LPSLoggerConfig
        {
            public string LogFilePath { get; set; }
            public LPSLoggingLevel ConsoleLogingLevel { get; set; }
            public bool EnableConsoleLogging { get; set; }
            public bool EnableConsoleErrorLogging { get; set; }
            public bool DisableFileLogging { get; set; }
            public LPSLoggingLevel LoggingLevel { get; set; }
        }

        public static IHostBuilder ConfigureLPSFileLogger(this IHostBuilder hostBuilder, LPSLoggerConfig lpsFileConfig = null)
        {
            hostBuilder.ConfigureServices((hostContext, services) =>
            {
                // Read the custom logger configuration from the appsettings.json file or any other configuration source
                if(lpsFileConfig == null)
                    lpsFileConfig = hostContext.Configuration.GetSection("LPSFileLoggerConfig").Get<LPSLoggerConfig>();

                // Create an instance of your custom logger implementation
                var fileLogger = new FileLogger(lpsFileConfig.LogFilePath);

                fileLogger.EnableConsoleLogging = lpsFileConfig.EnableConsoleLogging;
                fileLogger.EnableConsoleErrorLogging = lpsFileConfig.EnableConsoleErrorLogging;
                fileLogger.ConsoleLoggingLevel = lpsFileConfig.ConsoleLogingLevel;
                fileLogger.LoggingLevel = lpsFileConfig.LoggingLevel;
                fileLogger.DisableFileLogging = lpsFileConfig.DisableFileLogging;


                // Register the custom logger instance as a singleton in the DI container
                services.AddSingleton<ILPSLogger>(fileLogger);
                
            });
            return hostBuilder;
        }
    }


}
