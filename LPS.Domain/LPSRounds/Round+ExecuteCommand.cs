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

                var preparedClients = new List<(DateTime ExecutionTime, int StartupDelay,
                    List<(HttpIteration.ExecuteCommand Cmd, HttpIteration Iter)> Commands)>();
                var runClientsSequentially = this.RunClientsSequentially == true;

                try
                {
                    if (this.Stages != null && this.Stages.Count > 0)
                    {
                        awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Stages:  {this.Stages.Count}", LPSLoggingLevel.Verbose, token));

                        var startTime = DateTime.Now;
                        int cumulativeOffsetMs = 0;

                        if (!this.DelayClientCreationUntilIsNeeded.Value)
                        {
                            int totalClients = this.Stages.Sum(stage => stage.NumberOfClients);
                            for (int clientIndex = 0; clientIndex < totalClients; clientIndex++)
                            {
                                _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                            }
                        }

                        for (int stageIndex = 0; stageIndex < this.Stages.Count && !token.IsCancellationRequested; stageIndex++)
                        {
                            var stage = this.Stages[stageIndex];
                            cumulativeOffsetMs += stage.StartupDelay;

                            string stageDetails = runClientsSequentially
                                ? $"Stage {stageIndex + 1}/{this.Stages.Count}: {stage.NumberOfClients} sequential clients, arrival delay ignored"
                                : $"Stage {stageIndex + 1}/{this.Stages.Count}: {stage.NumberOfClients} clients, {stage.ArrivalDelay}ms arrival delay, scheduled at +{cumulativeOffsetMs}ms";
                            awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, stageDetails, LPSLoggingLevel.Verbose, token));

                            for (int clientIndex = 0; clientIndex < stage.NumberOfClients && !token.IsCancellationRequested; clientIndex++)
                            {
                                var executionTime = runClientsSequentially
                                    ? DateTime.Now
                                    : startTime.AddMilliseconds(cumulativeOffsetMs + clientIndex * stage.ArrivalDelay);
                                int startupDelay = runClientsSequentially && clientIndex == 0 ? stage.StartupDelay : 0;
                                preparedClients.Add((executionTime, startupDelay, PrepareClientCommands()));
                            }

                            int stageDurationMs = !runClientsSequentially && stage.NumberOfClients > 1 ? (stage.NumberOfClients - 1) * stage.ArrivalDelay : 0;
                            cumulativeOffsetMs += stageDurationMs;
                        }
                    }
                    else
                    {
                        awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbose, token));

                        if (!this.DelayClientCreationUntilIsNeeded.Value)
                        {
                            for (int clientIndex = 0; clientIndex < this.NumberOfClients; clientIndex++)
                            {
                                _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                            }
                        }

                        for (int clientIndex = 0; clientIndex < this.NumberOfClients && !token.IsCancellationRequested; clientIndex++)
                        {
                            int delayTime = runClientsSequentially ? 0 : clientIndex * (this.ArrivalDelay ?? 0);
                            preparedClients.Add((DateTime.Now.AddMilliseconds(delayTime), 0, PrepareClientCommands()));
                        }
                    }

                    token.ThrowIfCancellationRequested();
                    foreach (var client in preparedClients)
                    {
                        if (runClientsSequentially)
                        {
                            if (client.StartupDelay > 0)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(client.StartupDelay), token);
                            }
                            await ExecuteClientCommandsAsync(client.ExecutionTime, client.Commands, token);
                        }
                        else
                        {
                            awaitableTasks.Add(ExecuteClientCommandsAsync(client.ExecutionTime, client.Commands, token));
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
                            scheduled.Cmd.CancelIfScheduled();
                        }
                    }
                    throw;
                }
            }
        }

        private List<(HttpIteration.ExecuteCommand Cmd, HttpIteration Iter)> PrepareClientCommands()
        {
            var commands = new List<(HttpIteration.ExecuteCommand Cmd, HttpIteration Iter)>();
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
                    scheduled.Cmd.CancelIfScheduled();
                }
                throw;
            }
        }

        private async Task ExecuteClientCommandsAsync(DateTime executionTime,
            IReadOnlyList<(HttpIteration.ExecuteCommand Cmd, HttpIteration Iter)> commands, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var awaitableTasks = new List<Task>();

            foreach (var (cmd, iteration) in commands)
            {
                if (this.RunInParallel == true)
                {
                    awaitableTasks.Add(_httpIterationSchedulerService
                        .ScheduleAsync(executionTime, cmd, iteration, token));
                }
                else
                {
                    token.ThrowIfCancellationRequested();
                    await _httpIterationSchedulerService
                        .ScheduleAsync(executionTime, cmd, iteration, token);
                }
            }

            await Task.WhenAll(awaitableTasks);
            token.ThrowIfCancellationRequested();
        }
    }
}
