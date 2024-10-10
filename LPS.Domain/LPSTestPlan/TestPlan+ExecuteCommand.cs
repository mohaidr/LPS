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
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRun.LPSHttpRun.Scheduler;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace LPS.Domain
{

    public partial class TestPlan
    {
        IHttpRunSchedulerService _httpRunSchedulerService;
        public class ExecuteCommand : IAsyncCommand<TestPlan>
        {
            readonly ILogger _logger;
            readonly IWatchdog _watchdog;
            readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            readonly IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _lpsClientManager;
            readonly IClientConfiguration<HttpRequestProfile> _lpsClientConfig;
            readonly IMetricsDataMonitor _lpsMetricsDataMonitor;
            readonly ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
            readonly CancellationTokenSource _cts;
            protected ExecuteCommand()
            {
            }
            public ExecuteCommand(ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IClientManager<HttpRequestProfile, HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> lpsClientManager,
                IClientConfiguration<HttpRequestProfile> lpsClientConfig,
                ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunExecutionCommandStatusMonitor,
                IMetricsDataMonitor lpsMetricsDataMonitor,
                CancellationTokenSource cts)
            {
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsClientManager = lpsClientManager;
                _lpsClientConfig = lpsClientConfig;
                _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
                _lpsMetricsDataMonitor = lpsMetricsDataMonitor;
                _cts = cts;
            }
            private CommandExecutionStatus _executionStatus;
            public CommandExecutionStatus Status => _executionStatus;
            async public Task ExecuteAsync(TestPlan entity)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSTestPlan Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                entity._logger = this._logger;
                entity._watchdog = this._watchdog;
                entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;
                entity._lpsClientConfig = this._lpsClientConfig;
                entity._lpsClientManager = this._lpsClientManager;
                entity._lpsMetricsDataMonitor = this._lpsMetricsDataMonitor;
                entity._httpRunExecutionCommandStatusMonitor = this._httpRunExecutionCommandStatusMonitor;
                entity._cts = this._cts;
                entity._httpRunSchedulerService = new HttpRunSchedulerService(_logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _httpRunExecutionCommandStatusMonitor, _cts);
                await entity.ExecuteAsync(this);
            }

            //TODO:: When implementing IQueryable repository so you can run a subset of the defined Runs
            public IList<Guid> SelectedRuns { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        }
        async private Task ExecuteAsync(ExecuteCommand command)
        {
            if (this.IsValid && this._lPSRuns.Count > 0)
            {
                List<Task> awaitableTasks = new();
                #region Loggin Plan Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Details", LPSLoggingLevel.Verbose, _cts.Token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Name:  {this.Name}", LPSLoggingLevel.Verbose, _cts.Token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbose, _cts.Token));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbose, _cts.Token));
                #endregion


                RegisterHttpRunsForMonitor(); // Optionally pre-register HTTP runs for monitoring to include them in the dashboard immediately, even with empty execution lists, rather than waiting for each run to start.

                if (!this.DelayClientCreationUntilIsNeeded.Value)
                {
                    for (int i = 0; i < this.NumberOfClients; i++)
                    {
                        _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                    }
                }

                for (int i = 0; i < this.NumberOfClients && !_cts.Token.IsCancellationRequested; i++)
                {
                    IClientService<HttpRequestProfile, HttpResponse> httpClient;
                    if (!this.DelayClientCreationUntilIsNeeded.Value)
                    {
                        httpClient = _lpsClientManager.DequeueClient();
                    }
                    else
                    {
                        httpClient = _lpsClientManager.CreateInstance(_lpsClientConfig);
                    }
                    int delayTime = i * this.ArrivalDelay;
                    awaitableTasks.Add(SchedualHttpRunsForExecution(httpClient, DateTime.Now.AddMilliseconds(delayTime)));
                }
                await Task.WhenAll([..awaitableTasks]);
            }
        }

        private void RegisterHttpRunsForMonitor()
        {
            foreach (var run in this.LPSRuns)
            {
                if(run.Type == LPSRunType.HttpRun)
                _lpsMetricsDataMonitor.TryRegister((HttpRun)run);
            }
        }

        async Task SchedualHttpRunsForExecution(IClientService<HttpRequestProfile, HttpResponse> httpClient, DateTime executionTime)
        {
            List<Task> awaitableTasks = [];
            foreach (var httpRun in this.LPSRuns.Where(run=> run.Type == LPSRunType.HttpRun))
            {
                if (httpRun == null || !httpRun.IsValid)
                {
                    continue;
                }
                string hostName = new Uri(((HttpRun)httpRun).LPSHttpRequestProfile.URL).Host;
                await _watchdog.BalanceAsync(hostName, _cts.Token);
                if (this.RunInParallel.HasValue && this.RunInParallel.Value)
                {
                    awaitableTasks.Add(_httpRunSchedulerService.ScheduleHttpRunExecution(executionTime, (HttpRun)httpRun, httpClient));
                }
                else
                {
                    await _httpRunSchedulerService.ScheduleHttpRunExecution(executionTime, (HttpRun)httpRun, httpClient);
                }
            }
            await Task.WhenAll(awaitableTasks);
        }
    }
}

