using LPS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain
{
    public partial class LPSHttpTestCase
    {
        public class ExecuteCommand : IAsyncCommand<LPSHttpTestCase>
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
           
            ILPSClientService<LPSHttpRequest> _httpClientService;

            protected ExecuteCommand()
            {

            }
            public ExecuteCommand(ILPSClientService<LPSHttpRequest> httpClientService, LPSTestPlan.ExecuteCommand planExecCommand)
            {
                _httpClientService = httpClientService;
                LPSTestPlanExecuteCommand = planExecCommand;
            }

            async public Task ExecuteAsync(LPSHttpTestCase entity, ICancellationTokenWrapper cancellationTokenWrapper)
            {
                entity._httpClientService = this._httpClientService;
                await entity.ExecuteAsync(this, cancellationTokenWrapper);
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
               // List<Task> awaitableTasks = new List<Task>();

                #region Logging Request Details
                string logEntry = string.Empty;

                logEntry += "Test Case Details\n";
                logEntry += $"Id: {this.Id}\n";
                logEntry += $"Iteration Mode: {this.Mode}\n";
                logEntry += $"Request Count: {this.RequestCount}\n";
                logEntry += $"Duration: {this.Duration}\n";
                logEntry += $"Batch Size: {this.BatchSize}\n";
                logEntry += $"Cool Down Time: {this.CoolDownTime}\n";
                logEntry += $"Http Method: {this.LPSHttpRequest.HttpMethod.ToUpper()}\n";
                logEntry += $"Http Version: {this.LPSHttpRequest.Httpversion}\n";
                logEntry += $"URL: {this.LPSHttpRequest.URL}\n";

                if (!string.IsNullOrEmpty(this.LPSHttpRequest.Payload)
                    && (this.LPSHttpRequest.HttpMethod.ToUpper() == "PUT"
                    || this.LPSHttpRequest.HttpMethod.ToUpper() == "POST"
                    || this.LPSHttpRequest.HttpMethod.ToUpper() == "PATCH"))
                {
                    logEntry += "...Begin Request Body...\n";
                    logEntry += this.LPSHttpRequest.Payload + "\n";
                    logEntry += "...End Request Body...\n";
                }
                else
                {
                    logEntry += "...Empty Payload...\n";
                }

                if (this.LPSHttpRequest.HttpHeaders != null && this.LPSHttpRequest.HttpHeaders.Count>0)
                {
                    logEntry += "...Begin Request Headers...\n";

                    foreach (var header in this.LPSHttpRequest.HttpHeaders)
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

                LPSHttpRequest.ExecuteCommand lpsRequestExecCommand = new LPSHttpRequest.ExecuteCommand(this._httpClientService, command) ;
                TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
                _ = ReportAsync(command, this.Name, taskCompletionSource, cancellationTokenWrapper);
                Stopwatch stopwatch;
                int numberOfSentRequests = 0;
                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested)
                        {
                           // _resourceUsageTracker.Balance();
                            for (int b = 0; b < this.BatchSize && stopwatch.Elapsed.TotalSeconds < this.Duration.Value; b++)
                            {
                                _=lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationTokenWrapper);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds , cancellationTokenWrapper.CancellationToken);
                        }
                        stopwatch.Stop();
                        break;
                    case IterationMode.CRB:
                        for (int i = 0; i < this.RequestCount.Value && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested; i += this.BatchSize.Value)
                        {
                          //  _resourceUsageTracker.Balance();
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                _=lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationTokenWrapper);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds, cancellationTokenWrapper.CancellationToken);
                        }
                        break;
                    case IterationMode.CB:
                        while (!cancellationTokenWrapper.CancellationToken.IsCancellationRequested)
                        {
                           // _resourceUsageTracker.Balance();
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                _=lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationTokenWrapper);
                                numberOfSentRequests++;
                            }
                            await Task.Delay((int)TimeSpan.FromSeconds(this.CoolDownTime.Value).TotalMilliseconds, cancellationTokenWrapper.CancellationToken);
                        }
                        break;
                    case IterationMode.R:
                        for (int i = 0; i < this.RequestCount && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested; i++)
                        {
                           // _resourceUsageTracker.Balance();
                            _ = lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationTokenWrapper);
                            numberOfSentRequests++;
                        }
                        break;
                    case IterationMode.D:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested)
                        {
                            _resourceUsageTracker.Balance();
                            _ = lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationTokenWrapper);
                            numberOfSentRequests++;
                        }
                        stopwatch.Stop();
                        break;
                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has sent {numberOfSentRequests} request(s) to {this.LPSHttpRequest.URL}", LPSLoggingLevel.Information, cancellationTokenWrapper);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} is waiting for the {numberOfSentRequests} request(s) to complete", LPSLoggingLevel.Information, cancellationTokenWrapper);
                //await Task.WhenAll(awaitableTasks);

                while (command.NumberOfSuccessfullyCompletedRequests + NumberOfFailedCalls < numberOfSentRequests)
                {
                    await Task.Delay(5000);
                }

                taskCompletionSource.SetResult(true);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"The client {_httpClientService.Id} has completed all the requests to {this.LPSHttpRequest.URL} with {command.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {command.NumberOfFailedToCompleteRequests} failed to complete requests", LPSLoggingLevel.Information, cancellationTokenWrapper);
            }
        }

        //TODO: This logic has to be moved to a seprate reporting service in the infrastructure layer
        private async Task ReportAsync(ExecuteCommand execCommand, string name, TaskCompletionSource<bool> TaskCompletionSource, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Start reporting which will run every 10 Seconds", LPSLoggingLevel.Information, cancellationTokenWrapper);

            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            while (!TaskCompletionSource.Task.IsCompleted && !cancellationTokenWrapper.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10000, cancellationTokenWrapper.CancellationToken);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Client: {_httpClientService.Id} - Case: {name} - Successfully Completed: {execCommand.NumberOfSuccessfullyCompletedRequests} - Failed To Complete: {execCommand.NumberOfFailedToCompleteRequests}", LPSLoggingLevel.Information, cancellationTokenWrapper);
            }
        }
    }
}
