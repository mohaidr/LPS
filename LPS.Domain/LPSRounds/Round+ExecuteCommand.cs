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
        ISkippedRequestReporter _skippedRequestReporter;
        ICommandRepository<HttpIteration, IAsyncCommand<HttpIteration>> _httpIterationExecutionCommandRepository;
        IIterationStatusMonitor _iterationStatusMonitor;

        public class ExecuteCommand : IAsyncCommand<Round>
        {
            readonly ILogger _logger;
            readonly IWatchdog _watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            readonly ISkippedRequestReporter _skippedRequestReporter;
            readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
            readonly IClientConfiguration<HttpRequest> _lpsClientConfig;
            readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
            readonly IIterationStatusMonitor _iterationStatusMonitor;
            readonly ICommandRepository<HttpIteration, IAsyncCommand<HttpIteration>> _httpIterationExecutionCommandRepository;

            protected ExecuteCommand() { }

            public ExecuteCommand(
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                ISkippedRequestReporter skippedRequestReporter,
                IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> lpsClientManager,
                IClientConfiguration<HttpRequest> lpsClientConfig,
                ICommandRepository<HttpIteration, IAsyncCommand<HttpIteration>> httpIterationExecutionCommandRepository,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                IIterationStatusMonitor iterationStatusMonitor)
            {
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _skippedRequestReporter = skippedRequestReporter;
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
                entity._skippedRequestReporter = _skippedRequestReporter;
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
                    await Task.Delay(TimeSpan.FromMilliseconds(this.StartupDelay), token);
                }

                var awaitableTasks = new List<Task>
                {
                    _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round Details", LPSLoggingLevel.Verbose, token),
                    _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round Name:  {this.Name}", LPSLoggingLevel.Verbose, token),
                    _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbose, token),
                    _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Run Clients Sequentially:  {this.RunClientsSequentially}", LPSLoggingLevel.Verbose, token)
                };

                var preparedClients = new List<PreparedClient>();
                var runClientsSequentially = this.RunClientsSequentially == true;

                try
                {
                    var staged = this.Stages != null && this.Stages.Count > 0;
                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                        staged ? $"Stages:  {this.Stages.Count}" : $"Number Of Clients:  {this.NumberOfClients}",
                        LPSLoggingLevel.Verbose, token));

                    if (!this.DelayClientCreationUntilIsNeeded.Value)
                    {
                        var totalClients = staged ? this.Stages.Sum(stage => stage.NumberOfClients) : this.NumberOfClients;
                        for (int clientIndex = 0; clientIndex < totalClients; clientIndex++)
                        {
                            _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                        }
                    }

                    foreach (var schedule in RoundScheduleBuilder.Build(this))
                    {
                        token.ThrowIfCancellationRequested();
                        if (staged && schedule.ClientIndex == 0)
                        {
                            var stage = this.Stages[schedule.StageIndex];
                            string stageDetails = runClientsSequentially
                                ? $"Stage {schedule.StageIndex + 1}/{this.Stages.Count}: {stage.NumberOfClients} sequential clients, arrival delay ignored"
                                : $"Stage {schedule.StageIndex + 1}/{this.Stages.Count}: {stage.NumberOfClients} clients, {stage.ArrivalDelay}ms arrival delay, scheduled at +{schedule.ArrivalOffsetMs}ms";
                            awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, stageDetails, LPSLoggingLevel.Verbose, token));
                        }
                        preparedClients.Add(new PreparedClient(schedule, PrepareClientCommands()));
                    }

                    token.ThrowIfCancellationRequested();
                    var executionStart = DateTime.Now;
                    foreach (var client in preparedClients)
                    {
                        if (runClientsSequentially)
                        {
                            if (client.Schedule.StartupDelayMs > 0)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(client.Schedule.StartupDelayMs), token);
                            }
                            await ExecuteClientCommandsAsync(DateTime.Now, client.Commands, token);
                        }
                        else
                        {
                            awaitableTasks.Add(ExecuteClientCommandsAsync(executionStart.AddMilliseconds(client.Schedule.ArrivalOffsetMs), client.Commands, token));
                        }
                    }

                    await Task.WhenAll(awaitableTasks);
                }
                catch
                {
                    foreach (var client in preparedClients)
                    {
                        foreach (var scheduled in client.Commands)
                        {
                            scheduled.Command.CancelIfScheduled();
                        }
                    }
                    throw;
                }
            }
        }

        private List<(HttpIteration.ExecuteCommand Command, HttpIteration Iteration)> PrepareClientCommands()
        {
            var commands = new List<(HttpIteration.ExecuteCommand Command, HttpIteration Iteration)>();
            var httpClient = _lpsClientManager.DequeueClient() ?? _lpsClientManager.CreateInstance(_lpsClientConfig);

            try
            {
                foreach (var baseIteration in this.Iterations.Where(iteration => iteration.Type == IterationType.Http))
                {
                    var httpIteration = baseIteration as HttpIteration;
                    if (httpIteration == null || !httpIteration.IsValid)
                        continue;

                    var httpIterationCommand = new HttpIteration.ExecuteCommand(
                        httpClient,
                        _logger,
                        _watchdog,
                        _runtimeOperationIdProvider,
                        _skippedRequestReporter,
                        _lpsMetricsDataMonitor,
                        _iterationStatusMonitor);

                    commands.Add((httpIterationCommand, httpIteration));
                    _httpIterationExecutionCommandRepository.Add(httpIteration, httpIterationCommand);
                }
                return commands;
            }
            catch
            {
                foreach (var scheduled in commands)
                {
                    scheduled.Command.CancelIfScheduled();
                }
                throw;
            }
        }

        private async Task ExecuteClientCommandsAsync(DateTime executionTime,
            IReadOnlyList<(HttpIteration.ExecuteCommand Command, HttpIteration Iteration)> commands, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var awaitableTasks = new List<Task>();

            foreach (var (iterationCommand, iteration) in commands)
            {
                if (this.RunInParallel == true)
                {
                    awaitableTasks.Add(_httpIterationSchedulerService
                        .ScheduleAsync(executionTime, iterationCommand, iteration, token));
                }
                else
                {
                    token.ThrowIfCancellationRequested();
                    await _httpIterationSchedulerService
                        .ScheduleAsync(executionTime, iterationCommand, iteration, token);
                }
            }

            await Task.WhenAll(awaitableTasks);
            token.ThrowIfCancellationRequested();
        }
    }
}
