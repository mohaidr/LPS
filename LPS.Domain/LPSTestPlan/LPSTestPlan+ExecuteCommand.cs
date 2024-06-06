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
            protected ExecuteCommand()
            {
            }
            public ExecuteCommand(ILPSLogger logger,
                ILPSWatchdog watchdog,
                ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
                ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> lpsClientManager,
                ILPSClientConfiguration<LPSHttpRequestProfile> lpsClientConfig,
                ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> httpRunExecutionCommandStatusMonitor,
                ILPSMetricsDataMonitor lpsMonitoringEnroller)
            {
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsClientManager = lpsClientManager;
                _lpsClientConfig = lpsClientConfig;
                _httpRunExecutionCommandStatusMonitor = httpRunExecutionCommandStatusMonitor;
                _lpsMetricsDataMonitor = lpsMonitoringEnroller;
            }
            private AsyncCommandStatus _executionStatus;
            public AsyncCommandStatus Status => _executionStatus;
            async public Task ExecuteAsync(LPSTestPlan entity, ICancellationTokenWrapper cancellationTokenWrapper)
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
                await entity.ExecuteAsync(this, cancellationTokenWrapper);
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
        async private Task ExecuteAsync(ExecuteCommand command, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            if (this.IsValid && this._lPSHttpRuns.Count > 0)
            {
                List<Task> awaitableTasks = new List<Task>();
                #region Loggin Plan Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Details", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Name:  {this.Name}", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
                #endregion


                RegisterHttpRunsForMonitor(); // Optionally pre-register HTTP runs for monitoring to include them in the dashboard immediately, even with empty execution lists, rather than waiting for each run to start.

                if (!this.DelayClientCreationUntilIsNeeded.Value)
                {
                    for (int i = 0; i < this.NumberOfClients; i++)
                    {
                        _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                    }
                }



                for (int i = 0; i < this.NumberOfClients && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested; i++)
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
                    awaitableTasks.Add(LoopRunsAsync(command, httpClient, cancellationTokenWrapper));
                    if (this.RampUpPeriod > 0)
                    {
                        await Task.Delay(this.RampUpPeriod, cancellationTokenWrapper.CancellationToken);
                    }
                }
                await Task.WhenAll(awaitableTasks.ToArray());
            }
        }

        private void RegisterHttpRunsForMonitor()
        {
            foreach (var run in this.LPSHttpRuns)
            {
                _lpsMetricsDataMonitor.TryRegister(run);
            }
        }

        async Task LoopRunsAsync(ExecuteCommand command, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClient, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            List<Task> awaitableTasks = new List<Task>();
            foreach (var httpRun in this.LPSHttpRuns)
            {
                
                if (httpRun == null || !httpRun.IsValid)
                {
                    continue;
                }
                string hostName = new Uri(httpRun.LPSHttpRequestProfile.URL).Host;
                await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);
                if (this.RunInParallel.HasValue && this.RunInParallel.Value)
                {
                    awaitableTasks.Add(ExecuteRunAsync(httpRun, command, httpClient, cancellationTokenWrapper));
                }
                else
                {
                    await ExecuteRunAsync(httpRun, command, httpClient, cancellationTokenWrapper);
                }
            }
            await Task.WhenAll(awaitableTasks);
        }

        async Task ExecuteRunAsync(LPSHttpRun httpRun, ExecuteCommand command, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClient, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            LPSHttpRun.ExecuteCommand lpsHttpRunExecutecommand = new LPSHttpRun.ExecuteCommand(httpClient, command, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMetricsDataMonitor, _httpRunExecutionCommandStatusMonitor);
            _httpRunExecutionCommandStatusMonitor.RegisterCommand(lpsHttpRunExecutecommand, httpRun);
            LPSHttpRun httpRunClone = (LPSHttpRun)httpRun.Clone();
            _lpsMetricsDataMonitor?.Monitor(httpRun, lpsHttpRunExecutecommand.GetHashCode().ToString());
            await lpsHttpRunExecutecommand.ExecuteAsync(httpRunClone, cancellationTokenWrapper);
            _lpsMetricsDataMonitor?.Stop(httpRun, lpsHttpRunExecutecommand.GetHashCode().ToString());
        }
    }
}

