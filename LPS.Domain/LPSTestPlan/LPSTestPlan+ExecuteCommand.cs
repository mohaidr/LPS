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
                _numberOfSentRequests = 0;
            }

            private int _numberOfSentRequests;
            public int NumberOfSentRequests { get { return _numberOfSentRequests; } }
            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand command)
            {
                return Interlocked.Increment(ref command._numberOfSentRequests);
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
               

                for (int i = 0; i < this.NumberOfClients && !cancellationToken.IsCancellationRequested; i++)
                {
                    ILPSClientService<LPSHttpRequest> httpClientService = null;
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
                        await Task.Delay(this.RampUpPeriod, cancellationToken);
                    }
                }

                #region Local method to loop through the plan test cases and execute them async or sequentially 
                async Task ExecCaseAsync(ILPSClientService<LPSHttpRequest> httpClientService) // a race condition may happen here and causes the wrong httpClient to be captured if the httpClientService state changes before the await is called 
                {
                    foreach (var testCase in this.LPSTestCases)
                    {
                        LPSHttpTestCase.ExecuteCommand testCaseExecutecommand = new LPSHttpTestCase.ExecuteCommand(httpClientService, command);
                        LPSHttpTestCase cloneToTestCase = new LPSHttpTestCase(_logger, _runtimeOperationIdProvider); //use internal constructor designed for internal usage
                        testCase.Clone(cloneToTestCase);
                        if (this.RunInParallel.HasValue && this.RunInParallel.Value)
                        {
                            awaitableTasks.Add(testCaseExecutecommand.ExecuteAsync(cloneToTestCase, cancellationToken));
                        }
                        else
                        {
                            await testCaseExecutecommand.ExecuteAsync(cloneToTestCase, cancellationToken);
                        }
                    }
                }
                #endregion

                await Task.WhenAll(awaitableTasks.ToArray());
            }
        }
    }
}

