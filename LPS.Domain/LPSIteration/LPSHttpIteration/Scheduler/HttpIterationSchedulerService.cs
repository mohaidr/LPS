using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.LPSHttpIteration.Scheduler
{
    public class HttpIterationSchedulerService : IHttpIterationSchedulerService
    {
        readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        private readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
        readonly IWatchdog _watchdog;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly CancellationTokenSource _cts;
        readonly ILogger _logger;
        readonly ITerminationCheckerService _terminationCheckerService;
        public HttpIterationSchedulerService(ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandStatusMonitor,
                ITerminationCheckerService terminationCheckerService,
                CancellationTokenSource cts)
        {
            _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
            _cts = cts;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
            _terminationCheckerService = terminationCheckerService;
            _logger = logger;
        }

        public async Task ScheduleHttpIterationExecutionAsync(DateTime scheduledTime, HttpIteration httpIteration, IClientService<HttpRequest, HttpResponse> httpClient)
        {
            HttpIteration.ExecuteCommand httpIterationCommand = new(httpClient, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _terminationCheckerService, _cts);
            try
            {
                _httpIterationExecutionCommandStatusMonitor.Register(httpIterationCommand, httpIteration);

                var delayTime = (scheduledTime - DateTime.Now);
                if (delayTime > TimeSpan.Zero)
                {
                    await Task.Delay(delayTime, _cts.Token);
                }
                if (httpIteration.StartupDelay > 0)
                {
                  await Task.Delay(TimeSpan.FromSeconds(httpIteration.StartupDelay));
                }
                _lpsMetricsDataMonitor?.Monitor(httpIteration);
                await httpIterationCommand.ExecuteAsync(httpIteration);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Scheduled execution of '{httpIteration.Name}' has been cancelled", LPSLoggingLevel.Warning);
            }
            finally
            {
                _lpsMetricsDataMonitor?.Stop(httpIteration);
                httpIterationCommand.CancellIfScheduled();
            }
        }
    }
}
