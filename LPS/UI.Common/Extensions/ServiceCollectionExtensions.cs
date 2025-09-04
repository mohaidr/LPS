using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.PlaceHolderService.Methods;
using LPS.Infrastructure.PlaceHolderService;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void ConfigureWritable<T>(
            this IServiceCollection services,
            IConfigurationSection section,
            string appSettingsFileLocation) where T : class, new()
        {
            services.Configure<T>(section);
            _ = services.AddTransient<IWritableOptions<T>>(provider =>
            {
                var configuration = (IConfigurationRoot)provider.GetService<IConfiguration>();
                var environment = provider.GetService<IHostEnvironment>();
                var options = provider.GetService<IOptionsMonitor<T>>();
                return new WritableOptions<T>(environment, options, configuration, section.Path, appSettingsFileLocation);
            });
        }

        /// <summary>
        /// Registers placeholder methods and services.
        /// </summary>
        public static IServiceCollection AddPlaceholderResolution(this IServiceCollection services)
        {
            // Register Lazy<IPlaceholderResolverService> for MethodBase dependencies
            services.AddSingleton(provider => new Lazy<IPlaceholderResolverService>(() => provider.GetRequiredService<IPlaceholderResolverService>()));

            // Processor and resolver
            services.AddSingleton<IPlaceholderProcessor, PlaceholderProcessor>();
            services.AddSingleton<IPlaceholderResolverService, PlaceholderResolverService>();
            services.AddSingleton<ParameterExtractorService>();

            // Explicitly register TimestampMethod as itself for DateTimeAliasMethod dependency
            services.AddSingleton<TimestampMethod>();
            // Explicitly register IterateMethod as itself for LoopCounterAliasMethod dependency
            services.AddSingleton<IterateMethod>();

            // Methods (register every IPlaceholderMethod)
            services.AddSingleton<IPlaceholderMethod, RandomMethod>();
            services.AddSingleton<IPlaceholderMethod, RandomNumberMethod>();
            services.AddSingleton<IPlaceholderMethod, TimestampMethod>();
            // Register DateTimeAliasMethod with factory to inject TimestampMethod
            services.AddSingleton<IPlaceholderMethod>(provider => new DateTimeAliasMethod(provider.GetRequiredService<TimestampMethod>()));
            services.AddSingleton<IPlaceholderMethod, GuidMethod>();
            services.AddSingleton<IPlaceholderMethod, UuidMethod>();
            services.AddSingleton<IPlaceholderMethod, IterateMethod>();
            services.AddSingleton<IPlaceholderMethod>(provider => new LoopCounterAliasMethod(provider.GetRequiredService<IterateMethod>()));
            services.AddSingleton<IPlaceholderMethod, UrlEncodeMethod>();
            services.AddSingleton<IPlaceholderMethod, UrlDecodeMethod>();
            services.AddSingleton<IPlaceholderMethod, Base64EncodeMethod>();
            services.AddSingleton<IPlaceholderMethod, Base64DecodeMethod>();
            services.AddSingleton<IPlaceholderMethod, HashMethod>();
            services.AddSingleton<IPlaceholderMethod, JwtClaimMethod>();
            services.AddSingleton<IPlaceholderMethod, FormatMethod>();
            services.AddSingleton<IPlaceholderMethod, GenerateEmailMethod>();
            services.AddSingleton<IPlaceholderMethod, ReadMethod>();

            return services;
        }
    }
}
