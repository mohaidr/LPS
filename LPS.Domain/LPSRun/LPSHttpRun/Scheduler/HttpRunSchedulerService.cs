using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.LPSHttpRun.Scheduler
{
    public class HttpRunSchedulerService : IHttpRunSchedulerService
    {
        readonly ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
        private readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
        readonly IWatchdog _watchdog;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly CancellationTokenSource _cts;
        readonly ILogger _logger;
        public HttpRunSchedulerService(ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
                CancellationTokenSource cts)
        {
            _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
            _cts = cts;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _logger = logger;
        }

        public async Task ScheduleHttpRunExecution(DateTime scheduledTime, HttpRun httpRun, IClientService<HttpRequestProfile, HttpResponse> httpClient)
        {
            try
            {
                HttpRun.ExecuteCommand httpRunCommand = new(httpClient, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _cts);
                _httpRunExecutionCommandStatusMonitor.RegisterCommand(httpRunCommand, httpRun);

                var delayTime = (int)(scheduledTime - DateTime.Now).TotalMilliseconds;
                if (delayTime > 0)
                {
                    await Task.Delay(delayTime);
                }

                _lpsMetricsDataMonitor?.Monitor(httpRun, httpRunCommand.GetHashCode().ToString());
                await httpRunCommand.ExecuteAsync(httpRun);
                _lpsMetricsDataMonitor?.Stop(httpRun, httpRunCommand.GetHashCode().ToString());
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Scheduled execution of '{httpRun.Name}' has been cancelled", LPSLoggingLevel.Warning);
            }
        }
    }
}
