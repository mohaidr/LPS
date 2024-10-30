using Dashboard.Common;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.UI.Common.Options;
using LPS.UI.Core.Host;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class LPSManager
    {
        readonly ILogger _logger;
        readonly IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _httpClientManager;
        readonly IClientConfiguration<HttpRequestProfile> _config;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly IWatchdog _watchdog;
        readonly IMetricsDataMonitor _lpsMonitoringEnroller;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        readonly CancellationTokenSource _cts;
        IOptions<DashboardConfigurationOptions> _dashboardConfig;
        internal LPSManager(ILogger logger,
                IClientManager<HttpRequestProfile, HttpResponse,IClientService<HttpRequestProfile, HttpResponse>> httpClientManager,
                IClientConfiguration<HttpRequestProfile> config,
                IWatchdog wtahcdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
                IMetricsDataMonitor lpsMonitoringEnroller,
                IOptions<DashboardConfigurationOptions> dashboardConfig,
                CancellationTokenSource cts)
        {
            _logger = logger;
            _httpClientManager = httpClientManager;
            _config = config;
            _watchdog = wtahcdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _dashboardConfig = dashboardConfig;
            _lpsMonitoringEnroller = lpsMonitoringEnroller;
            _cts = cts;
        }
        public async Task RunAsync(Plan plan)
        {
            var count = plan.GetReadOnlyRounds().Count();
            if (plan!=null && plan.IsValid && count > 0)
            {
                if (_dashboardConfig.Value.BuiltInDashboard.HasValue && _dashboardConfig.Value.BuiltInDashboard.Value)
                {
                    var port = _dashboardConfig.Value?.Port ?? GlobalSettings.Port;
                    var queryParams = $"pullevery={_dashboardConfig.Value?.PullEvery ?? 5}";
                    Host.Dashboard.Start(port, queryParams);
                }
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round '{plan?.Name}' execution has started", LPSLoggingLevel.Information);
                await new Plan.ExecuteCommand(_logger, _watchdog, _runtimeOperationIdProvider, _httpClientManager, _config, _httpIterationExecutionCommandStatusMonitor, _lpsMonitoringEnroller, _cts)
                    .ExecuteAsync(plan);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round '{plan?.Name}' execution has completed", LPSLoggingLevel.Information);
            }
        }

    }
}
