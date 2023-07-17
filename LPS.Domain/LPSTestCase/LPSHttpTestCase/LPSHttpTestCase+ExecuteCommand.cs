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
        new public class ExecuteCommand : IAsyncCommand<LPSHttpTestCase>
        {
            private readonly ProtectedAccessTestExecuteCommand protectedAccessTestExecuteCommand = new ProtectedAccessTestExecuteCommand();
            private class ProtectedAccessTestExecuteCommand : LPSTestPlan.ExecuteCommand
            {
                public new int SafelyIncrementNumberofSentRequests(LPSTestPlan.ExecuteCommand dto)
                {
                    return base.SafelyIncrementNumberofSentRequests(dto);
                }
            }
            public LPSTestPlan.ExecuteCommand LPSTestPlanExecuteCommand { get; set; }
            ILPSClientService<LPSHttpRequest> _httpClient;

            protected ExecuteCommand()
            {

            }
            public ExecuteCommand(ILPSClientService<LPSHttpRequest> httpClient)
            {
                _httpClient = httpClient;
                LPSTestPlanExecuteCommand = new LPSTestPlan.ExecuteCommand();
            }

            async public Task ExecuteAsync(LPSHttpTestCase entity, CancellationToken cancellationToken)
            {
                entity._httpClient = this._httpClient;
                await entity.ExecuteAsync(this, cancellationToken);
            }

            private int _numberOfSuccessfullyCompletedRequests;
            private int _numberOfFailedToCompleteRequests;
            private int _numberOfSentRequests;

            protected int SafelyIncrementNumberOfSuccessfulRequests(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberOfSuccessfullyCompletedRequests);
            }

            protected int SafelyIncrementNumberOfFailedRequests(ExecuteCommand execCommand)
            {
                return Interlocked.Increment(ref execCommand._numberOfFailedToCompleteRequests);
            }

            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand execCommand)
            {
                protectedAccessTestExecuteCommand.SafelyIncrementNumberofSentRequests(execCommand.LPSTestPlanExecuteCommand);
                return Interlocked.Increment(ref execCommand._numberOfSentRequests);
            }

            public int NumberOfSentRequests { get { return _numberOfSentRequests; } }
            public int NumberOfSuccessfullyCompletedRequests { get { return _numberOfSuccessfullyCompletedRequests; } }
            public int NumberOfFailedToCompleteRequests { get { return _numberOfFailedToCompleteRequests; } }
        }
        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken cancellationToken)
        {
            if (this.IsValid)
            {
                List<Task> awaitableTasks = new List<Task>();

                #region Logging Request Details
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Request Details", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Iteration Mode:  {this.Mode}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Request Count:  {this.RequestCount}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Duration:  {this.Duration}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Batch Size:  {this.BatchSize}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Cool Down Time:  {this.CoolDownTime}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Http Method:  {this.LPSHttpRequest.HttpMethod.ToUpper()}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Http Version: {this.LPSHttpRequest.Httpversion}", LPSLoggingLevel.Verbos));
                awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"URL: {this.LPSHttpRequest.URL}", LPSLoggingLevel.Verbos));
                if (!string.IsNullOrEmpty(this.LPSHttpRequest.Payload)
                    && (this.LPSHttpRequest.HttpMethod.ToUpper() == "PUT"
                    || this.LPSHttpRequest.HttpMethod.ToUpper() == "POST"
                    || this.LPSHttpRequest.HttpMethod.ToUpper() == "PATCH"))
                {
                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "...Begin Request Body...", LPSLoggingLevel.Verbos));
                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, this.LPSHttpRequest.Payload, LPSLoggingLevel.Verbos));
                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "...End Request Body...", LPSLoggingLevel.Verbos));
                }
                else
                {
                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "...Empty Payload...", LPSLoggingLevel.Verbos));
                }

                if (this.LPSHttpRequest.HttpHeaders != null)
                {
                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "...Begin Request Headers...", LPSLoggingLevel.Verbos));

                    foreach (var header in this.LPSHttpRequest.HttpHeaders)
                    {
                        awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"{header.Key}: {header.Value}", LPSLoggingLevel.Verbos));
                    }

                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "...End Request Headers...", LPSLoggingLevel.Verbos));
                }
                else
                {
                    awaitableTasks.Add(_logger.LogAsync(_runtimeOperationIdProvider.OperationId, "...No Headers Were Provided...", LPSLoggingLevel.Verbos));
                }
                #endregion


                LPSHttpRequest.ExecuteCommand lpsRequestExecCommand = new LPSHttpRequest.ExecuteCommand(this._httpClient) { LPSTestCaseExecuteCommand = command };
                TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
                var reportTask = ReportAsync(command, taskCompletionSource, this.LPSHttpRequest.URL, cancellationToken);
                Stopwatch stopwatch;
                int numberOfSentRequests = 0;
                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !cancellationToken.IsCancellationRequested)
                        {
                            for (int b = 0; b < this.BatchSize && stopwatch.Elapsed.TotalSeconds < this.Duration.Value; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                                numberOfSentRequests++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        stopwatch.Stop();
                        break;
                    case IterationMode.CRB:
                        for (int i = 0; i < this.RequestCount.Value && !cancellationToken.IsCancellationRequested; i += this.BatchSize.Value)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                                numberOfSentRequests++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        break;
                    case IterationMode.CB:
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                                numberOfSentRequests++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        break;
                    case IterationMode.R:
                        for (int i = 0; i < this.RequestCount && !cancellationToken.IsCancellationRequested; i++)
                        {
                            awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                            numberOfSentRequests++;
                        }
                        break;
                    case IterationMode.D:
                        stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !cancellationToken.IsCancellationRequested)
                        {
                            awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                            numberOfSentRequests++;
                        }
                        stopwatch.Stop();
                        break;
                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Number of sent requests: {numberOfSentRequests}", LPSLoggingLevel.Information);
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Wating for the {numberOfSentRequests} request to complete", LPSLoggingLevel.Information);
                await Task.WhenAll(awaitableTasks);
                taskCompletionSource.SetResult(true);
                await reportTask;
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"All requests has been processed by {this.LPSHttpRequest.URL} with {command.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {command.NumberOfFailedToCompleteRequests} failed to complete requests", LPSLoggingLevel.Information);
            }
        }

        //TODO: This logic has to be moved to a seprate reporting service in the infrastructure layer
        private async Task ReportAsync(ExecuteCommand dto, TaskCompletionSource<bool> TaskCompletionSource, string url, CancellationToken cancellationToken)
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            await Task.Delay(5000);
            while (!TaskCompletionSource.Task.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"\tHost: {url}, Successfully completed: {dto.NumberOfSuccessfullyCompletedRequests}, Faile to complete:{dto.NumberOfFailedToCompleteRequests}", LPSLoggingLevel.Information);
                await Task.Delay(5000);
            }
        }
    }
}
