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
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpRunExecutionCommandStatusMonitor;
        private readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
        readonly IWatchdog _watchdog;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly CancellationTokenSource _cts;
        readonly ILogger _logger;
        public HttpRunSchedulerService(ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpRunExecutionCommandStatusMonitor,
                CancellationTokenSource cts)
        {
            _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
            _cts = cts;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
            _logger = logger;
        }

        public async Task ScheduleHttpRunExecution(DateTime scheduledTime, HttpIteration httpRun, IClientService<HttpRequestProfile, HttpResponse> httpClient)
        {
            try
            {
                HttpIteration.ExecuteCommand httpIterationCommand = new(httpClient, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _cts);
                _httpRunExecutionCommandStatusMonitor.RegisterCommand(httpIterationCommand, httpRun);

                var delayTime = (int)(scheduledTime - DateTime.Now).TotalMilliseconds;
                if (delayTime > 0)
                {
                    await Task.Delay(delayTime);
                }

                _lpsMetricsDataMonitor?.Monitor(httpRun, httpIterationCommand.GetHashCode().ToString());
                await httpIterationCommand.ExecuteAsync(httpRun);
                _lpsMetricsDataMonitor?.Stop(httpRun, httpIterationCommand.GetHashCode().ToString());
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Scheduled execution of '{httpRun.Name}' has been cancelled", LPSLoggingLevel.Warning);
            }
        }
    }
}
