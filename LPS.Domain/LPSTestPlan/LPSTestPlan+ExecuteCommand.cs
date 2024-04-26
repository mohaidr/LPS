using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
            private ILPSClientManager<LPSHttpRequestProfile, LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _lpsClientManager;
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
            if (this.IsValid && this._lPSHttpRuns.Count > 0)
            {
                List<Task> awaitableTasks = new List<Task>();
                List<ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClients = new List<ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>>();
                #region Loggin Plan Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Details", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Name:  {this.Name}", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbose, cancellationTokenWrapper));
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
                    if (!this.DelayClientCreationUntilIsNeeded.Value)
                    {
                        httpClients.Add(_lpsClientManager.DequeueClient());
                    }
                    else
                    {
                        httpClients.Add(_lpsClientManager.CreateInstance(_lpsClientConfig));
                    }
                }

                foreach (var run in this.LPSHttpRuns)
                {
                    awaitableTasks.Add(ExecHttpRunAsync(run, command, httpClients, cancellationTokenWrapper));
                }

                await Task.WhenAll(awaitableTasks.ToArray());
            }
        }

        async Task ExecHttpRunAsync(LPSHttpRun httpRun, ExecuteCommand command, List<ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> httpClients, ICancellationTokenWrapper cancellationTokenWrapper) // a race condition may happen here and causes the wrong httpClient to be captured if the httpClientService state changes before the "await" is called 
        {
            List<Task> localAwaitableTasks = new List<Task>();

           _lpsMonitoringEnroller?.Enroll(httpRun, _logger, _runtimeOperationIdProvider);
            foreach (var client in httpClients)
            {
                string hostName = new Uri(httpRun.LPSHttpRequestProfile.URL).Host;
                await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);

                LPSHttpRun.ExecuteCommand lpsHttpRunExecutecommand = new LPSHttpRun.ExecuteCommand(client, command, _logger, _watchdog, _runtimeOperationIdProvider, _lpsMonitoringEnroller);
                LPSHttpRun httpRunClone = (LPSHttpRun)httpRun.Clone();
                localAwaitableTasks.Add(lpsHttpRunExecutecommand.ExecuteAsync(httpRunClone, cancellationTokenWrapper));
                if (this.RampUpPeriod > 0)
                {
                    await Task.Delay(this.RampUpPeriod, cancellationTokenWrapper.CancellationToken);
                }
            }
            await Task.WhenAll(localAwaitableTasks.ToArray());

            _lpsMonitoringEnroller?.Withdraw(httpRun, _logger, _runtimeOperationIdProvider);
        }
    }
}

