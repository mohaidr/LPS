using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
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
            private ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse,ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _lpsClientManager;
            private ILPSClientConfiguration<LPSHttpRequestProfile> _lpsClientConfig;
            ILPSMonitoringEnroller _lpsMonitoringEnroller;
            protected ExecuteCommand()
            { 
            }
            public ExecuteCommand(ILPSLogger logger,
                ILPSWatchdog watchdog,
                ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
                ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> lpsClientManager,
                ILPSClientConfiguration<LPSHttpRequestProfile> lpsClientConfig,
                ILPSMonitoringEnroller lpsMonitoringEnroller)
            {
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsClientManager = lpsClientManager;
                _lpsClientConfig = lpsClientConfig;
                _lpsMonitoringEnroller = lpsMonitoringEnroller;
            }
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
                entity._lpsMonitoringEnroller = this._lpsMonitoringEnroller;
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
            if (this.IsValid)
            {
                List<Task> awaitableTasks = new List<Task>();

                #region Loggin Plan Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Details", LPSLoggingLevel.Verbos, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Name:  {this.Name}", LPSLoggingLevel.Verbos, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbos, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbos, cancellationTokenWrapper));
                #endregion

                if (!this.DelayClientCreationUntilIsNeeded.Value)
                {
                    for (int i = 0; i < this.NumberOfClients; i++)
                    {
                        _lpsClientManager.CreateAndQueueClient(_lpsClientConfig);
                    }
                }
                for (int i = 0; i < this.NumberOfClients && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested; i++)
                {
                    ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClientService = null;
                    if (!this.DelayClientCreationUntilIsNeeded.Value)
                    {
                        httpClientService = _lpsClientManager.DequeueClient();
                    }
                    else
                    {
                        httpClientService = _lpsClientManager.CreateInstance(_lpsClientConfig);
                    }

                    awaitableTasks.Add(ExecCaseAsync(httpClientService));
                    if (this.RampUpPeriod>0)
                    { 
                        await Task.Delay(this.RampUpPeriod, cancellationTokenWrapper.CancellationToken);
                    }
                }

                #region Local method to loop through the plan test runs and execute them async or sequentially 
                async Task ExecCaseAsync(ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClientService) // a race condition may happen here and causes the wrong httpClient to be captured if the httpClientService state changes before the "await" is called 
                {
                    foreach (var run in this.LPSHttpRuns)
                    {
                        if (run == null || !run.IsValid)
                        {
                            continue;
                        }
                        string hostName = new Uri(run.LPSHttpRequestProfile.URL).Host;
                        await _watchdog.Balance(hostName);
                        LPSHttpRun.ExecuteCommand lpsHttpRunExecutecommand = new LPSHttpRun.ExecuteCommand(httpClientService, command, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMonitoringEnroller);
                        LPSHttpRun httpRunClone = (LPSHttpRun)run.Clone();
                        if (this.RunInParallel.HasValue && this.RunInParallel.Value)
                        {
                            awaitableTasks.Add(lpsHttpRunExecutecommand.ExecuteAsync(httpRunClone, cancellationTokenWrapper));
                        }
                        else
                        {
                            await lpsHttpRunExecutecommand.ExecuteAsync(httpRunClone, cancellationTokenWrapper);
                        }
                    }
                }
                #endregion

                await Task.WhenAll(awaitableTasks.ToArray());
            }
        }
    }
}

