using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRun.LPSHttpIteration.Scheduler;

namespace LPS.Domain
{
    public partial class Round
    {
        IHttpIterationSchedulerService _httpIterationSchedulerService;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
        IClientConfiguration<HttpRequest> _lpsClientConfig;
        IWatchdog _watchdog;
        ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandRepository;
        IIterationStatusMonitor _iterationStatusMonitor;

        public class ExecuteCommand : IAsyncCommand<Round>
        {
            readonly ILogger _logger;
            readonly IWatchdog _watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
            readonly IClientConfiguration<HttpRequest> _lpsClientConfig;
            readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
            readonly IIterationStatusMonitor _iterationStatusMonitor;
            readonly ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandRepository;

            protected ExecuteCommand() { }

            public ExecuteCommand(
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> lpsClientManager,
                IClientConfiguration<HttpRequest> lpsClientConfig,
                ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandRepository,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                IIterationStatusMonitor iterationStatusMonitor)
            {
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsClientManager = lpsClientManager;
                _lpsClientConfig = lpsClientConfig;
                _httpIterationExecutionCommandRepository = httpIterationExecutionCommandRepository;
                _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
                _iterationStatusMonitor = iterationStatusMonitor;
            }

            private CommandExecutionStatus _executionStatus;
            public CommandExecutionStatus Status => _executionStatus;

            public async Task ExecuteAsync(Round entity, CancellationToken token)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Round Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }

                entity._logger = _logger;
                entity._watchdog = _watchdog;
                entity._runtimeOperationIdProvider = _runtimeOperationIdProvider;
                entity._lpsClientConfig = _lpsClientConfig;
                entity._lpsClientManager = _lpsClientManager;
                entity._lpsMetricsDataMonitor = _lpsMetricsDataMonitor;
                entity._httpIterationExecutionCommandRepository = _httpIterationExecutionCommandRepository;
                entity._iterationStatusMonitor = _iterationStatusMonitor;

                entity._httpIterationSchedulerService = new HttpIterationSchedulerService(
                    _logger,
                    _watchdog,
                    _runtimeOperationIdProvider,
                    _lpsMetricsDataMonitor,
                    _iterationStatusMonitor,
                    _lpsClientManager,
                    _lpsClientConfig);

                await entity.ExecuteAsync(this, token);
            }

            public IList<Guid> SelectedRuns { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        }

        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken token)
        {
            if (this.IsValid && this.Iterations.Count > 0)
            {
                if (this.StartupDelay > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(this.StartupDelay), token);
                }

                var awaitableTasks = new List<Task>();

                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round Details", LPSLoggingLevel.Verbose, token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round Name:  {this.Name}", LPSLoggingLevel.Verbose, token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbose, token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbose, token));

                if (!this.DelayClientCreationUntilIsNeeded.Value)
                {
                    for (int i = 0; i < this.NumberOfClients; i++)
                    {
                        _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                    }
                }

                for (int i = 0; i < this.NumberOfClients && !token.IsCancellationRequested; i++)
                {
                    int delayTime = i * (this.ArrivalDelay ?? 0);
                    awaitableTasks.Add(SchedualHttpIterationForExecutionAsync(DateTime.Now.AddMilliseconds(delayTime), token));
                }

                await Task.WhenAll(awaitableTasks);
            }
        }

        private async Task SchedualHttpIterationForExecutionAsync(DateTime executionTime, CancellationToken token)
        {
            // Preregister all commands so the iteration status can reflect the status correctly as it assumes all commands are registered. -> this should change but doing it for now to keep the development effort
            var commandQueue = new Queue<(HttpIteration.ExecuteCommand Cmd, HttpIteration Iter)>();

            foreach (var baseIteration in this.Iterations.Where(iteration => iteration.Type == IterationType.Http))
            {
                var httpIteration = baseIteration as HttpIteration;
                if (httpIteration == null || !httpIteration.IsValid)
                    continue;

                var httpClient = _lpsClientManager.DequeueClient() ?? _lpsClientManager.CreateInstance(_lpsClientConfig);

                var httpIterationCommand = new HttpIteration.ExecuteCommand(
                    httpClient,
                    _logger,
                    _watchdog,
                    _runtimeOperationIdProvider,
                    _lpsMetricsDataMonitor,
                    _iterationStatusMonitor);

                _httpIterationExecutionCommandRepository.Register(httpIterationCommand, httpIteration);

                commandQueue.Enqueue((httpIterationCommand, httpIteration));
            }

            // Second loop: dequeue & schedule
            var awaitableTasks = new List<Task>();

            while (commandQueue.Count > 0)
            {
                var (cmd, iteration) = commandQueue.Dequeue();

                if (this.RunInParallel == true)
                {
                    awaitableTasks.Add(_httpIterationSchedulerService
                        .ScheduleAsync(executionTime, cmd, iteration, token));
                }
                else
                {
                    await _httpIterationSchedulerService
                        .ScheduleAsync(executionTime, cmd, iteration, token);
                }
            }

            await Task.WhenAll(awaitableTasks);
        }
    }
}
