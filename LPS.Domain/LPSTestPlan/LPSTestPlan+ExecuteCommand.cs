using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;
using Newtonsoft.Json;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class ExecuteCommand : IAsyncCommand<LPSTestPlan>
        {
            async public Task ExecuteAsync(LPSTestPlan entity, CancellationToken cancellationToken)
            {
                Reset();
                await entity.ExecuteAsync(this, cancellationToken);
            }

            private void Reset()
            {
                _numberofSentRequests = 0;
            }

            private int _numberofSentRequests;
            public int NumberOfSentRequests { get { return _numberofSentRequests; } }
            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberofSentRequests);
            }
        }
        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken cancellationToken)
        {
            if (this.IsValid)
            {
                List<Task> awaitableTasks = new List<Task>();
                #region Loggin Plan Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Details", LPSLoggingLevel.Verbos, cancellationToken));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Plan Name:  {this.Name}", LPSLoggingLevel.Verbos, cancellationToken));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number Of Clients:  {this.NumberOfClients}", LPSLoggingLevel.Verbos, cancellationToken));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Delay Client Creation:  {this.DelayClientCreationUntilIsNeeded}", LPSLoggingLevel.Verbos, cancellationToken));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client Timeout:  {this.ClientTimeout}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Max Connections Per Server:  {this.MaxConnectionsPerServer}", LPSLoggingLevel.Verbos, cancellationToken));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Pooled Connection Idle Timeout:  {this.PooledConnectionIdleTimeout}", LPSLoggingLevel.Verbos, cancellationToken));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Pooled Connection Life Time:  {this.PooledConnectionLifeTime}", LPSLoggingLevel.Verbos, cancellationToken));
                #endregion

                ILPSClientService<LPSHttpRequest> httpClientService = null;

                async Task ExecCaseAsync()
                {
                    LPSHttpTestCase.ExecuteCommand testCaseExecutecommand = new LPSHttpTestCase.ExecuteCommand(httpClientService) { LPSTestPlanExecuteCommand = command };
                    foreach (var testCase in this.LPSTestCases)
                    {
                        LPSHttpTestCase cloneTestCase = new LPSHttpTestCase(_logger, _runtimeOperationIdProvider);
                        testCase.Clone(cloneTestCase);
                        if (this.RunInParallel.HasValue && this.RunInParallel.Value)
                        {
                            awaitableTasks.Add(testCaseExecutecommand.ExecuteAsync(cloneTestCase, cancellationToken));
                        }
                        else
                        {
                            await testCaseExecutecommand.ExecuteAsync(cloneTestCase, cancellationToken);
                        }
                    }
                }

                for (int i = 0; i < this.NumberOfClients && !cancellationToken.IsCancellationRequested; i++)
                {
                    if (!this.DelayClientCreationUntilIsNeeded.Value)
                    {
                        httpClientService = _lpsClientManager.DequeueClient();
                    }
                    else
                    {
                        httpClientService = _lpsClientManager.CreateInstance(_lpsClientConfig);
                    }

                    awaitableTasks.Add(ExecCaseAsync());

                    if (this.RampUpPeriod>0)
                    { 
                        await Task.Delay(this.RampUpPeriod, cancellationToken);
                    }
                }
                await Task.WhenAll(awaitableTasks.ToArray());
            }
        }
    }
}

