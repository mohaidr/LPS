using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
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

            // Register Lazy<IIfEvaluator> so predicate-based methods (e.g. FindMethod) can reuse the
            // shared expression evaluator without creating a DI cycle (evaluator -> resolver -> processor -> methods).
            services.AddSingleton(provider => new Lazy<IExpressionEvaluator>(() => provider.GetRequiredService<IExpressionEvaluator>()));

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
            services.AddSingleton<IPlaceholderMethod, HmacMethod>();
            services.AddSingleton<IPlaceholderMethod, ToLowerCaseMethod>();
            services.AddSingleton<IPlaceholderMethod, ToUpperCaseMethod>();
            services.AddSingleton<IPlaceholderMethod, ContainsMethod>();
            services.AddSingleton<IPlaceholderMethod, StartsWithMethod>();
            services.AddSingleton<IPlaceholderMethod, EndsWithMethod>();
            services.AddSingleton<IPlaceholderMethod, LengthMethod>();
            services.AddSingleton<IPlaceholderMethod, RandomItemMethod>();
            services.AddSingleton<IPlaceholderMethod, JwtClaimMethod>();
            services.AddSingleton<IPlaceholderMethod, JwtSignMethod>();
            services.AddSingleton<IPlaceholderMethod, FormatMethod>();
            services.AddSingleton<IPlaceholderMethod, StrcatMethod>();
            services.AddSingleton<IPlaceholderMethod, GenerateEmailMethod>();
            services.AddSingleton<IPlaceholderMethod, ReadMethod>();
            services.AddSingleton<IPlaceholderMethod, FindMethod>();

            // Numeric / comparison declarative methods
            services.AddSingleton<IPlaceholderMethod, SetVariableMethod>();
            services.AddSingleton<IPlaceholderMethod, SumMethod>();
            services.AddSingleton<IPlaceholderMethod, MinMethod>();
            services.AddSingleton<IPlaceholderMethod, MaxMethod>();
            services.AddSingleton<IPlaceholderMethod, MultiplyMethod>();
            services.AddSingleton<IPlaceholderMethod, AverageMethod>();
            services.AddSingleton<IPlaceholderMethod, DivideMethod>();
            services.AddSingleton<IPlaceholderMethod, SubtractMethod>();
            services.AddSingleton<IPlaceholderMethod, ModMethod>();
            services.AddSingleton<IPlaceholderMethod, PowMethod>();
            services.AddSingleton<IPlaceholderMethod, AbsMethod>();
            services.AddSingleton<IPlaceholderMethod, FloorMethod>();
            services.AddSingleton<IPlaceholderMethod, CeilMethod>();
            services.AddSingleton<IPlaceholderMethod, RoundMethod>();
            services.AddSingleton<IPlaceholderMethod, ClampMethod>();
            services.AddSingleton<IPlaceholderMethod, GreaterThanMethod>();
            services.AddSingleton<IPlaceholderMethod, SmallerThanMethod>();
            services.AddSingleton<IPlaceholderMethod, GreaterThanOrEqualMethod>();
            services.AddSingleton<IPlaceholderMethod, SmallerThanOrEqualMethod>();
            services.AddSingleton<IPlaceholderMethod, GreaterMethod>();
            services.AddSingleton<IPlaceholderMethod, LessMethod>();
            services.AddSingleton<IPlaceholderMethod, EqualMethod>();
            services.AddSingleton<IPlaceholderMethod, StringEqualsMethod>();

            return services;
        }
    }
}
