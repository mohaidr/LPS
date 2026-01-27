using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LPS.Infrastructure.Logger;
using LPS.Domain;
using LPS.UI.Common.Extensions;
using LPS.UI.Common;
using LPS.UI.Common.Options;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.UI.Core.Host;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Status;
using LPS.Infrastructure.LPSClients;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Caching;
using LPS.Infrastructure.LPSClients.HeaderServices;
using LPS.Infrastructure.LPSClients.URLServices;
using LPS.Infrastructure.LPSClients.MessageServices;
using LPS.Infrastructure.LPSClients.ResponseService;
using LPS.Infrastructure.LPSClients.SampleResponseServices;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Nodes;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.Services;
using LPS.Common.Services;
using LPS.Common.Interfaces;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Entity;
using System.Reflection;
using Newtonsoft.Json;
using LPS.Infrastructure.Services;
using LPS.Infrastructure.Monitoring.TerminationServices;
using LPS.Infrastructure.FailureEvaluator;
using LPS.Infrastructure.Monitoring;  // For MetricFetcher
using LPS.Infrastructure.Skip;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices;
using LPS.Infrastructure.Monitoring.MetricsVariables;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.PlaceHolderService;

namespace LPS
{
    static class Startup
    {
        public static IHost ConfigureServices(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {

                webBuilder.UseStartup<Apis.Startup>();
                var contentRoot = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
                ArgumentNullException.ThrowIfNull(contentRoot);
                webBuilder.UseContentRoot(contentRoot);

                var webRoot = Path.Combine(contentRoot, "wwwroot");
                webBuilder.UseWebRoot(webRoot);

                // Load configuration to access DashboardConfigurationOptions for the Port setting
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(AppConstants.AppSettingsFileLocation, optional: false, reloadOnChange: false)
                    .Build();

                var dashboardOptions = configuration
                    .GetSection("LPSAppSettings:Dashboard")
                    .Get<DashboardConfigurationOptions>();

                var port = dashboardOptions?.Port ?? GlobalSettings.DefaultDashboardPort;

                var clusterOptions = configuration
                    .GetSection("LPSAppSettings:Cluster")
                    .Get<ClusterConfigurationOptions>();

                var gRPCPort = (clusterOptions != null && new ClusteredConfigurationValidator().Validate(clusterOptions).IsValid) ? clusterOptions.GRPCPort.Value : GlobalSettings.DefaultGRPCPort;

                webBuilder.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.ListenAnyIP(gRPCPort, listenOptions =>
                    {
                        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
                    });
                    serverOptions.ListenAnyIP(port);
                    serverOptions.AllowSynchronousIO = false;

                })
                .UseShutdownTimeout(TimeSpan.FromSeconds(30));
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
                services.AddSingleton<ICustomGrpcClientFactory, CustomGrpcClientFactory>();
                services.AddSingleton<IWarmUpService, WarmUpService>();
                services.AddSingleton<IEntityRepositoryService, EntityRepositoryService>();
                services.AddSingleton<IEntityDiscoveryService, EntityDiscoveryService>();
                services.AddSingleton<IRuleService, RuleService>();
                services.AddSingleton<IMetricAggregatorFactory, MetricAggregatorFactory>();
                services.AddSingleton<IMetricsUiService, MetricsUiService>();
                services.AddSingleton<ILiveMetricDataStore, LiveMetricDataStore>();
                services.AddSingleton<IWindowedMetricDataStore, WindowedMetricDataStore>();
                services.AddSingleton<ICumulativeMetricDataStore, CumulativeMetricDataStore>();
                services.AddSingleton<IPlanExecutionContext, PlanExecutionContext>();
                services.AddSingleton<IMetricsDataMonitor, MetricsDataMonitor>();
                services.AddSingleton<IMetricsVariableService, MetricsVariableService>();
                services.AddSingleton<IIterationStatusMonitor, IterationStatusMonitor>();
                services.AddSingleton<ICommandStatusMonitor<HttpIteration>, HttpIterationCommandStatusMonitor>();
                services.AddSingleton<AppSettingsWritableOptions>();
                services.AddSingleton<CancellationTokenSource>();
                services.AddSingleton<IConsoleLogger, ConsoleLogger>();
                services.AddSingleton<ILogFormatter, LogFormatter>();
                services.AddSingleton<ICacheService<IHttpResponseVariableHolder>, MemoryCacheService<IHttpResponseVariableHolder>>();
                services.AddSingleton<ICacheService<string>, MemoryCacheService<string>>();
                services.AddSingleton<ICacheService<long>, MemoryCacheService<long>>();
                services.AddSingleton<ICacheService<object>, MemoryCacheService<object>>();
                services.AddSingleton(new MemoryCache(new MemoryCacheOptions
                {
                    SizeLimit = 1024
                }));
                services.AddSingleton<INodeRegistry, NodeRegistry>();

                services.AddSingleton<ICommandRepository<HttpIteration, IAsyncCommand<HttpIteration>>, InMemoryCommandRepository<HttpIteration, IAsyncCommand<HttpIteration>>>();


                services.AddSingleton<ITestTriggerNotifier, TestTriggerNotifier>();
                services.AddSingleton<INodeMetadata, NodeMetadata>();
                services.AddSingleton<IClientManager<Domain.HttpRequest, Domain.HttpResponse, IClientService<Domain.HttpRequest, Domain.HttpResponse>>, HttpClientManager>();
                services.AddSingleton<IRuntimeOperationIdProvider, RuntimeOperationIdProvider>();
                services.AddSingleton<IHttpHeadersService, HttpHeadersService>();
                services.AddSingleton<IMetricsService, MetricsService>();
                services.AddSingleton<IUrlSanitizationService, UrlSanitizationService>();
                services.AddSingleton<IMessageService, MessageService>();
                services.AddSingleton<IResponseProcessingService, ResponseProcessingService>();
                services.AddSingleton<IResponsePersistenceFactory, ResponsePersistenceFactory>();
                services.AddPlaceholderResolution();
                services.AddSingleton<ISessionManager, SessionManager>();
                services.AddSingleton<IVariableManager, VariableManager>();
                services.AddSingleton<ITestExecutionService, TestExecutionService>();
                services.AddSingleton<ITestOrchestratorService, TestOrchestratorService>();

                // NEW: Register MetricFetcher for shared metric retrieval
                services.AddSingleton<IMetricFetcher, MetricFetcher>();

                services.AddSingleton<ITerminationCheckerService, HttpIterationTerminationCheckerService>();
                services.AddSingleton<IIterationFailureEvaluator, IterationFailureEvaluator>();
                services.AddSingleton<ISkipIfEvaluator, SkipIfEvaluator>();
                services.AddSingleton<IVariableFactory, VariableFactory>();
                services.AddSingleton<IDashboardService, DashboardService>();
                services.AddSingleton<NodeHealthMonitorBackgroundService>();

                // Windowed metrics - new clean architecture
                // Coordinator fires OnWindowClosed event, aggregators subscribe and push to queue
                // Pusher reads from queue and sends to SignalR (registered in Apis project)
                services.AddSingleton<IWindowedMetricsQueue, WindowedMetricsQueue>();
                services.AddSingleton<IWindowedMetricsCoordinator>(sp =>
                {
                    // Read refresh rate from config, default to 3 seconds
                    var dashboardOptions = hostContext.Configuration
                        .GetSection("LPSAppSettings:Dashboard")
                        .Get<DashboardConfigurationOptions>();
                    var refreshRateSeconds = dashboardOptions?.RefreshRate ?? 3;
                    var coordinator = new WindowedMetricsCoordinator(TimeSpan.FromSeconds(refreshRateSeconds));
                    return coordinator;
                });

                // Cumulative metrics - separate coordinator with same refresh interval
                // This pushes cumulative data (cards/summary) at RefreshRate interval
                services.AddSingleton<ICumulativeMetricsQueue, CumulativeMetricsQueue>();
                services.AddSingleton<ICumulativeMetricsCoordinator>(sp =>
                {
                    // Read refresh rate from config, default to 3 seconds
                    var dashboardOptions = hostContext.Configuration
                        .GetSection("LPSAppSettings:Dashboard")
                        .Get<DashboardConfigurationOptions>();
                    var refreshRateSeconds = dashboardOptions?.RefreshRate ?? 3;
                    var coordinator = new CumulativeMetricsCoordinator(refreshRateSeconds);
                    return coordinator;
                });

                services.ConfigureWritable<DashboardConfigurationOptions>(hostContext.Configuration.GetSection("LPSAppSettings:Dashboard"), AppConstants.AppSettingsFileLocation);
                services.ConfigureWritable<FileLoggerOptions>(hostContext.Configuration.GetSection("LPSAppSettings:FileLogger"), AppConstants.AppSettingsFileLocation);
                services.ConfigureWritable<WatchdogOptions>(hostContext.Configuration.GetSection("LPSAppSettings:Watchdog"), AppConstants.AppSettingsFileLocation);
                services.ConfigureWritable<HttpClientOptions>(hostContext.Configuration.GetSection("LPSAppSettings:HttpClient"), AppConstants.AppSettingsFileLocation);
                services.ConfigureWritable<ClusterConfigurationOptions>(hostContext.Configuration.GetSection("LPSAppSettings:Cluster"), AppConstants.AppSettingsFileLocation);
                services.ConfigureWritable<LPS.UI.Common.Options.InfluxDBOptions>(hostContext.Configuration.GetSection("LPSAppSettings:InfluxDB"), AppConstants.AppSettingsFileLocation);
                services.AddHostedService(isp => isp.ResolveWith<HostedService>(new { args }));

                if (hostContext.HostingEnvironment.IsProduction())
                {
                    //add production dependencies
                }
                else
                {
                    // add development dependencies
                }
            })
            .UseFileLogger()
            .UseWatchdog()
            .UseHttpClient()
            .UseClusterConfiguration()
            .UseInfluxDB()
            .ConfigureLogging(logging =>
            {
                // Suppress verbose System.Net.Http logging - only show warnings and errors
                logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            })
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
