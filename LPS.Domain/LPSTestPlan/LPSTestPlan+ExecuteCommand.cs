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
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class ExecuteCommand : IAsyncCommand<LPSTestPlan>
        {
            private ILPSLogger _logger;
            private ILPSWatchdog _watchdog;
            private ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            private ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _lpsClientManager;
            private ILPSClientConfiguration<LPSHttpRequestProfile> _lpsClientConfig;
            ILPSMetricsDataMonitor _lpsMetricsDataMonitor;
            ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> _httpRunExecutionCommandStatusMonitor;
            CancellationTokenSource _cts;
            protected ExecuteCommand()
            {
            }
            public ExecuteCommand(ILPSLogger logger,
                ILPSWatchdog watchdog,
                ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
                ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> lpsClientManager,
                ILPSClientConfiguration<LPSHttpRequestProfile> lpsClientConfig,
                ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> httpRunExecutionCommandStatusMonitor,
                ILPSMetricsDataMonitor lpsMonitoringEnroller,
                CancellationTokenSource cts)
            {
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsClientManager = lpsClientManager;
                _lpsClientConfig = lpsClientConfig;
                _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
                _lpsMetricsDataMonitor = lpsMonitoringEnroller;
                _cts = cts;
            }
            private AsyncCommandStatus _executionStatus;
            public AsyncCommandStatus Status => _executionStatus;
            async public Task ExecuteAsync(LPSTestPlan entity)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSTestPlan Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                Reset();
                entity._logger = this._logger;
                entity._watchdog = this._watchdog;
                entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;
                entity._lpsClientConfig = this._lpsClientConfig;
                entity._lpsClientManager = this._lpsClientManager;
                entity._lpsMetricsDataMonitor = this._lpsMetricsDataMonitor;
                entity._httpRunExecutionCommandStatusMonitor = this._httpRunExecutionCommandStatusMonitor;
                entity._cts = this._cts;
                await entity.ExecuteAsync(this);
            }

            private void Reset()
            {
                _numberOfSentRequests = 0;
            }

            private int _numberOfSentRequests;
            public int NumberOfSentRequests { get { return _numberOfSentRequests; } }
            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand command)
            {
                return Interlocked.Increment(ref command._numberOfSentRequests);
            }

            //TODO:: When implementing IQueryable repository so you can run a subset of the defined Runs
            public IList<Guid> SelectedRuns { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        }
        async private Task ExecuteAsync(ExecuteCommand command)
        {
            if (this.IsValid && this._lPSRuns.Count > 0)
            {
                List<Task> awaitableTasks = new List<Task>();
                #region Loggin Plan Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Details", LPSLoggingLevel.Verbose, _cts));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Name:  {this.Name}", LPSLoggingLevel.Verbose, _cts));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbose, _cts));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbose, _cts));
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
                    ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClient = null;
                    if (!this.DelayClientCreationUntilIsNeeded.Value)
                    {
                        httpClient = _lpsClientManager.DequeueClient();
                    }
                    else
                    {
                        httpClient = _lpsClientManager.CreateInstance(_lpsClientConfig);
                    }
                    awaitableTasks.Add(LoopRunsAsync(command, httpClient));
                    if (this.RampUpPeriod > 0)
                    {
                        await Task.Delay(this.RampUpPeriod, _cts.Token);
                    }
                }
                await Task.WhenAll(awaitableTasks.ToArray());
            }
        }

        private void RegisterHttpRunsForMonitor()
        {
            foreach (var run in this.LPSRuns)
            {
                if(run.Type == LPSRunType.HttpRun)
                _lpsMetricsDataMonitor.TryRegister((LPSHttpRun)run);
            }
        }

        async Task LoopRunsAsync(ExecuteCommand command, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClient)
        {
            List<Task> awaitableTasks = new List<Task>();
            foreach (var httpRun in this.LPSRuns.Where(run=> run.Type == LPSRunType.HttpRun))
            {
                
                if (httpRun == null || !httpRun.IsValid)
                {
                    continue;
                }
                string hostName = new Uri(((LPSHttpRun)httpRun).LPSHttpRequestProfile.URL).Host;
                await _watchdog.BalanceAsync(hostName);
                if (this.RunInParallel.HasValue && this.RunInParallel.Value)
                {
                    awaitableTasks.Add(ExecuteRunAsync(((LPSHttpRun)httpRun), command, httpClient));
                }
                else
                {
                    await ExecuteRunAsync(((LPSHttpRun)httpRun), command, httpClient);
                }
            }
            await Task.WhenAll(awaitableTasks);
        }

        async Task ExecuteRunAsync(LPSHttpRun httpRun, ExecuteCommand command, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClient)
        {
            LPSHttpRun.ExecuteCommand lpsHttpRunExecutecommand = new LPSHttpRun.ExecuteCommand(httpClient, command, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _httpRunExecutionCommandStatusMonitor, _cts);
            _httpRunExecutionCommandStatusMonitor.RegisterCommand(lpsHttpRunExecutecommand, httpRun);
            LPSHttpRun httpRunClone = (LPSHttpRun)httpRun.Clone();
            _lpsMetricsDataMonitor?.Monitor(httpRun, lpsHttpRunExecutecommand.GetHashCode().ToString());
            await lpsHttpRunExecutecommand.ExecuteAsync(httpRunClone);
            _lpsMetricsDataMonitor?.Stop(httpRun, lpsHttpRunExecutecommand.GetHashCode().ToString());
        }
    }
}

