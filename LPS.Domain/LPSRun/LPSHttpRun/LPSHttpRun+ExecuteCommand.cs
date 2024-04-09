using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain
{
    public partial class LPSHttpRun
    {
        public class ExecuteCommand : IAsyncCommand<LPSHttpRun>
        {
            private readonly ProtectedAccessTestPlanExecuteCommand protectedAccessTestPlanExecuteCommand = new ProtectedAccessTestPlanExecuteCommand();
            private class ProtectedAccessTestPlanExecuteCommand : LPSTestPlan.ExecuteCommand
            {
                public new int SafelyIncrementNumberofSentRequests(LPSTestPlan.ExecuteCommand command)
                {
                    return base.SafelyIncrementNumberofSentRequests(command);
                }
            }
            public LPSTestPlan.ExecuteCommand LPSTestPlanExecuteCommand { get; set; }
           
            ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> _httpClientService;
            ILPSLogger _logger;
            ILPSWatchdog _watchdog;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            ILPSMonitoringEnroller _lpsMonitoringEnroller;
            protected ExecuteCommand()
            {

            }
            public ExecuteCommand(ILPSClientService<LPSHttpRequestProfile
                , LPSHttpResponse> httpClientService, 
                LPSTestPlan.ExecuteCommand planExecCommand,
                ILPSLogger logger,
                ILPSWatchdog watchdog,
                ILPSRuntimeOperationIdProvider runtimeOperationIdProvider,
                ILPSMonitoringEnroller lpsMonitoringEnroller)
            {
                _httpClientService = httpClientService;
                LPSTestPlanExecuteCommand = planExecCommand;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsMonitoringEnroller = lpsMonitoringEnroller;
            }
            bool _isExecutionCompleted = false;
            async public Task ExecuteAsync(LPSHttpRun entity, ICancellationTokenWrapper cancellationTokenWrapper)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSHttpRun Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                entity._httpClientService = this._httpClientService;
                entity._logger = this._logger;
                entity._watchdog = this._watchdog;
                entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;
                entity._lpsMonitoringEnroller = this._lpsMonitoringEnroller;
                await entity.ExecuteAsync(this, cancellationTokenWrapper);
                _isExecutionCompleted = true;
            }

            private int _numberOfSuccessfullyCompletedRequests;
            private int _numberOfFailedToCompleteRequests;
            private int _numberOfSentRequests;
            public int NumberOfSentRequests { get { return _numberOfSentRequests; } }
            public int NumberOfSuccessfullyCompletedRequests { get { return _numberOfSuccessfullyCompletedRequests; } }
            public int NumberOfFailedToCompleteRequests { get { return _numberOfFailedToCompleteRequests; } }

            protected int SafelyIncrementNumberOfSuccessfulRequests(ExecuteCommand execCommand)
            {
              return Interlocked.Increment(ref execCommand._numberOfSuccessfullyCompletedRequests);
            }

            protected int SafelyIncrementNumberOfFailedRequests(ExecuteCommand execCommand)
            {
                return Interlocked.Increment(ref execCommand._numberOfFailedToCompleteRequests);
            }

            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand execCommand)
            {
                protectedAccessTestPlanExecuteCommand.SafelyIncrementNumberofSentRequests(execCommand.LPSTestPlanExecuteCommand);
                return Interlocked.Increment(ref execCommand._numberOfSentRequests);
            }
        }
        async private Task ExecuteAsync(ExecuteCommand command, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            if (this.IsValid)
            {

                #region Logging Request Details
                string logEntry = string.Empty;

                logEntry += "Run Details\n";
                logEntry += $"Id: {this.Id}\n";
                logEntry += $"Iteration Mode: {this.Mode}\n";
                logEntry += $"Request Count: {this.RequestCount}\n";
                logEntry += $"Duration: {this.Duration}\n";
                logEntry += $"Batch Size: {this.BatchSize}\n";
                logEntry += $"Cool Down Time: {this.CoolDownTime}\n";
                logEntry += $"Http Method: {this.LPSHttpRequestProfile.HttpMethod.ToUpper()}\n";
                logEntry += $"Http Version: {this.LPSHttpRequestProfile.Httpversion}\n";
                logEntry += $"URL: {this.LPSHttpRequestProfile.URL}\n";

                if (!string.IsNullOrEmpty(this.LPSHttpRequestProfile.Payload)
                    && (this.LPSHttpRequestProfile.HttpMethod.ToUpper() == "PUT"
                    || this.LPSHttpRequestProfile.HttpMethod.ToUpper() == "POST"
                    || this.LPSHttpRequestProfile.HttpMethod.ToUpper() == "PATCH"))
                {
                    logEntry += "...Begin Request Body...\n";
                    logEntry += this.LPSHttpRequestProfile.Payload + "\n";
                    logEntry += "...End Request Body...\n";
                }
                else
                {
                    logEntry += "...Empty Payload...\n";
                }

                if (this.LPSHttpRequestProfile.HttpHeaders != null && this.LPSHttpRequestProfile.HttpHeaders.Count>0)
                {
                    logEntry += "...Begin Request Headers...\n";

                    foreach (var header in this.LPSHttpRequestProfile.HttpHeaders)
                    {
                        logEntry += $"{header.Key}: {header.Value}\n";
                    }

                    logEntry += "...End Request Headers...\n";
                }
                else
                {
                    logEntry += "...No Headers Were Provided...\n";
                }

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, logEntry, LPSLoggingLevel.Verbos, cancellationTokenWrapper);
                #endregion

                LPSHttpRequestProfile.ExecuteCommand lpsRequestProfileExecCommand = new LPSHttpRequestProfile.ExecuteCommand(this._httpClientService, command, _logger, _watchdog, _runtimeOperationIdProvider) ;
                TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
                Stopwatch stopwatch;
                int numberOfSentRequests = 0;
                string hostName = new Uri(this.LPSHttpRequestProfile.URL).Host;
                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested)
                        {
                            for (int b = 0; b < this.BatchSize && stopwatch.Elapsed.TotalSeconds < this.Duration.Value; b++)
                            {
                                _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile, cancellationTokenWrapper);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds , cancellationTokenWrapper.CancellationToken);
                           await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);
                        }
                        stopwatch.Stop();
                        break;
                    case IterationMode.CRB:
                        for (int i = 0; i < this.RequestCount.Value && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested; i += this.BatchSize.Value)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                _ = lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile, cancellationTokenWrapper);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds, cancellationTokenWrapper.CancellationToken);
                            await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);
                        }
                        break;
                    case IterationMode.CB:
                        while (!cancellationTokenWrapper.CancellationToken.IsCancellationRequested)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                _=lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile, cancellationTokenWrapper);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds, cancellationTokenWrapper.CancellationToken);
                            await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);
                        }
                        break;
                    case IterationMode.R:
                        for (int i = 0; i < this.RequestCount && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested; i++)
                        {
                            await lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile, cancellationTokenWrapper);
                            numberOfSentRequests++;
                            await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);
                        }
                        break;
                    case IterationMode.D:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested)
                        {
                            await lpsRequestProfileExecCommand.ExecuteAsync(LPSHttpRequestProfile, cancellationTokenWrapper);
                            numberOfSentRequests++;
                            await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);
                        }
                        stopwatch.Stop();
                        break;
                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }


                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has sent {numberOfSentRequests} request(s) to {this.LPSHttpRequestProfile.URL}", LPSLoggingLevel.Information, cancellationTokenWrapper);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} is waiting for the {numberOfSentRequests} request(s) to complete", LPSLoggingLevel.Information, cancellationTokenWrapper);

                //TODO: Change this logic to event driven to avoid unnecessary conext switching every 1 second
                //Also the approach of knowing if the test has completed by counters may not be the best so look for some other solution
                while (command.NumberOfSuccessfullyCompletedRequests + command.NumberOfFailedToCompleteRequests < numberOfSentRequests)
                {
                    await Task.Delay(1000);
                }

                taskCompletionSource.SetResult(true);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has completed all the requests to {this.LPSHttpRequestProfile.URL} with {command.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {command.NumberOfFailedToCompleteRequests} failed to complete requests", LPSLoggingLevel.Information, cancellationTokenWrapper);
            }
        }
    }
}
