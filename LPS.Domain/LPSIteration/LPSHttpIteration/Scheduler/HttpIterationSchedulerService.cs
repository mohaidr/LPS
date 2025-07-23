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
        readonly ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandRepository;
        private readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
        readonly IWatchdog _watchdog;
        readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        readonly ILogger _logger;
        readonly IIterationStatusMonitor _iterationStatusMonitor;
        readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
        readonly IClientConfiguration<HttpRequest> _lpsClientConfig;
        public HttpIterationSchedulerService(ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandRepository,
                IIterationStatusMonitor iterationStatusMonitor,
                IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> lpsClientManager,
                IClientConfiguration<HttpRequest> lpsClientConfig)
        {
            _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
            _watchdog = watchdog;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _httpIterationExecutionCommandRepository = httpIterationExecutionCommandRepository;
            _iterationStatusMonitor = iterationStatusMonitor;
            _lpsClientManager = lpsClientManager;
            _lpsClientConfig = lpsClientConfig;
            _logger = logger;
        }

        public async Task ScheduleAsync(DateTime scheduledTime, HttpIteration httpIteration, CancellationToken token)
        {
            IClientService<HttpRequest, HttpResponse> httpClient = _lpsClientManager.DequeueClient() ?? _lpsClientManager.CreateInstance(_lpsClientConfig);
            HttpIteration.ExecuteCommand httpIterationCommand = new(httpClient, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _iterationStatusMonitor);
            try
            {
                _httpIterationExecutionCommandRepository.Register(httpIterationCommand, httpIteration);
                var delayTime = (scheduledTime - DateTime.Now);
                if (delayTime > TimeSpan.Zero)
                {
                    await Task.Delay(delayTime, token);
                }
                if (httpIteration.StartupDelay > 0)
                {
                  await Task.Delay(TimeSpan.FromSeconds(httpIteration.StartupDelay), token);
                }
                _lpsMetricsDataMonitor?.Monitor(httpIteration);
                await httpIterationCommand.ExecuteAsync(httpIteration, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Scheduled execution of '{httpIteration.Name}' has been cancelled", LPSLoggingLevel.Warning);
            }
            finally
            {
                _lpsMetricsDataMonitor?.Stop(httpIteration);
               // httpIterationCommand.CancellIfScheduled();
            }
        }
    }
}
