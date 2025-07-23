using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRun.LPSHttpIteration.Scheduler;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace LPS.Domain
{

    public partial class Round
    {
        IHttpIterationSchedulerService _httpIterationSchedulerService;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
        IClientConfiguration<HttpRequest> _lpsClientConfig;
        IWatchdog _watchdog;
        ICommandStatusMonitor<HttpIteration> _httpIterationExecutionCommandStatusMonitor;

        public class ExecuteCommand : IAsyncCommand<Round>
        {
            readonly ILogger _logger;
            readonly IWatchdog _watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            readonly IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> _lpsClientManager;
            readonly IClientConfiguration<HttpRequest> _lpsClientConfig;
            readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
            readonly ICommandStatusMonitor<HttpIteration> _httpIterationExecutionCommandStatusMonitor;
            readonly IIterationStatusMonitor _iterationStatusMonitor;
            readonly ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandRepository;
            protected ExecuteCommand()
            {
            }
            public ExecuteCommand(ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>> lpsClientManager,
                IClientConfiguration<HttpRequest> lpsClientConfig,
                ICommandStatusMonitor<HttpIteration> httpIterationExecutionCommandStatusMonitor,
                ICommandRepository<IAsyncCommand<HttpIteration>, HttpIteration> httpIterationExecutionCommandRepository,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                IIterationStatusMonitor iterationStatusMonitor)
            {
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsClientManager = lpsClientManager;
                _lpsClientConfig = lpsClientConfig;
                _httpIterationExecutionCommandStatusMonitor = httpIterationExecutionCommandStatusMonitor;
                _httpIterationExecutionCommandRepository = httpIterationExecutionCommandRepository;
                _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
                _iterationStatusMonitor = iterationStatusMonitor;
            }
            private CommandExecutionStatus _executionStatus;
            public CommandExecutionStatus Status => _executionStatus;
            async public Task ExecuteAsync(Round entity, CancellationToken token)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Round Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                entity._logger = this._logger;
                entity._watchdog = this._watchdog;
                entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;
                entity._lpsClientConfig = this._lpsClientConfig;
                entity._lpsClientManager = this._lpsClientManager;
                entity._lpsMetricsDataMonitor = this._lpsMetricsDataMonitor;
                entity._httpIterationExecutionCommandStatusMonitor = this._httpIterationExecutionCommandStatusMonitor;
                entity._httpIterationSchedulerService = new HttpIterationSchedulerService(_logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _httpIterationExecutionCommandRepository, _iterationStatusMonitor, _lpsClientManager, _lpsClientConfig);
                await entity.ExecuteAsync(this, token);
            }

            //TODO:: When implementing IQueryable repository so you can run a subset of the defined Runs
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

                List<Task> awaitableTasks = new();
                #region Loggin Round Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round Details", LPSLoggingLevel.Verbose, token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Round Name:  {this.Name}", LPSLoggingLevel.Verbose, token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbose, token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbose, token));
                #endregion

                if (!this.DelayClientCreationUntilIsNeeded.Value)
                {
                    for (int i = 0; i < this.NumberOfClients; i++)
                    {
                        _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                    }
                }

                for (int i = 0; i < this.NumberOfClients && !token.IsCancellationRequested; i++)
                {
                    int delayTime = i * (this.ArrivalDelay?? 0);
                    awaitableTasks.Add(SchedualHttpIterationForExecutionAsync(DateTime.Now.AddMilliseconds(delayTime), token));
                }
                await Task.WhenAll([..awaitableTasks]);
            }
        }

        private async Task SchedualHttpIterationForExecutionAsync(DateTime executionTime, CancellationToken token)
        {
            List<Task> awaitableTasks = [];
            foreach (var httpIteration in this.Iterations.Where(iteration=> iteration.Type == IterationType.Http))
            {
                if (httpIteration == null || !httpIteration.IsValid)
                {
                    continue;
                }
                if (this.RunInParallel.HasValue && this.RunInParallel.Value)
                {
                    awaitableTasks.Add(_httpIterationSchedulerService.ScheduleAsync(executionTime, (HttpIteration)httpIteration, token));
                }
                else
                {
                    await _httpIterationSchedulerService.ScheduleAsync(executionTime, (HttpIteration)httpIteration, token);
                }
            }
            await Task.WhenAll(awaitableTasks);
        }
    }
}

