using System;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.Domain.LPSRun.LPSHttpIteration.Scheduler
{
    public class HttpIterationSchedulerService : IHttpIterationSchedulerService
    {
        private readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
        private readonly IWatchdog _watchdog;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly ILogger _logger;
        private readonly IIterationStatusMonitor _iterationStatusMonitor;
        private readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
        private readonly IClientConfiguration<HttpRequest> _lpsClientConfig;

        public HttpIterationSchedulerService(
            ILogger logger,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsDataMonitor lpsMetricsDataMonitor,
            IIterationStatusMonitor iterationStatusMonitor,
            IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> lpsClientManager,
            IClientConfiguration<HttpRequest> lpsClientConfig)
        {
            _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _iterationStatusMonitor = iterationStatusMonitor;
            _lpsClientManager = lpsClientManager;
            _lpsClientConfig = lpsClientConfig;
            _logger = logger;
        }

        public async Task ScheduleAsync(
            DateTime scheduledTime,
            HttpIteration.ExecuteCommand httpIterationCommand,
            HttpIteration httpIteration,
            CancellationToken token)
        {
            try
            {
                var delayTime = (scheduledTime - DateTime.Now);
                if (delayTime > TimeSpan.Zero)
                {
                    await Task.Delay(delayTime, token);
                }

                if (httpIteration.StartupDelay > 0) // Do not add this to the delay time, in the parallel case, it is complete mess as the client is already running and once completing Iteration 1 and starts iteration 2, the scheduled time is already in the past and the startup dealy will not be respected. 
                {
                    await Task.Delay(TimeSpan.FromSeconds(httpIteration.StartupDelay), token);
                }

                _lpsMetricsDataMonitor?.Monitor(httpIteration);
                await httpIterationCommand.ExecuteAsync(httpIteration, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Scheduled execution of '{httpIteration.Name}' has been cancelled", LPSLoggingLevel.Warning);
            }
            finally
            {
                _lpsMetricsDataMonitor?.Stop(httpIteration);
            }
        }
    }
}
