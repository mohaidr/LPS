using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTestCase
    {
        public class ExecuteCommand : IAsyncCommand<LPSTestCase>
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

            public ExecuteCommand()
            {
                LPSTestPlanExecuteCommand = new LPSTestPlan.ExecuteCommand();
            }

            async public Task ExecuteAsync(LPSTestCase entity, CancellationToken cancellationToken)
            {
                await entity.ExecuteAsync(this, cancellationToken);
            }

            private int _numberOfSuccessfullyCompletedRequests;
            private int _numberOfFailedToCompleteRequests;
            private int _numberOfSentRequests;

            protected int SafelyIncrementNumberOfSuccessfulRequests(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberOfSuccessfullyCompletedRequests);
            }

            protected int SafelyIncrementNumberOfFailedRequests(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberOfFailedToCompleteRequests);
            }

            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand dto)
            {
                protectedAccessTestExecuteCommand.SafelyIncrementNumberofSentRequests(dto.LPSTestPlanExecuteCommand);
                return Interlocked.Increment(ref dto._numberOfSentRequests);
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
                awaitableTasks.Add(_logger.LogAsync(string.Empty, "Request Details", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Request Count:  {this.RequestCount}", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Client Http Request Timeout: {this.LPSRequest.HttpRequestTimeout}", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Method:  {this.LPSRequest.HttpMethod.ToUpper()}", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Version: {this.LPSRequest.Httpversion}", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"URL: {this.LPSRequest.URL}", LoggingLevel.INF));
                if (!string.IsNullOrEmpty(this.LPSRequest.Payload) && (this.LPSRequest.HttpMethod.ToUpper() == "PUT" || this.LPSRequest.HttpMethod.ToUpper() == "POST" || this.LPSRequest.HttpMethod.ToUpper() == "PATCH"))
                {
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Body...", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, this.LPSRequest.Payload, LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Body...", LoggingLevel.INF));
                }
                else
                {
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Empty Payload...", LoggingLevel.INF));
                }

                if (this.LPSRequest.HttpHeaders != null)
                {
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Headers...", LoggingLevel.INF));

                    foreach (var header in this.LPSRequest.HttpHeaders)
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, $"{header.Key}: {header.Value}", LoggingLevel.INF));
                    }

                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Headers...", LoggingLevel.INF));
                }
                else
                {
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...No Headers Were Provided...", LoggingLevel.INF));
                }
                #endregion

                int requestsCounter;
                LPSRequest.ExecuteCommand lpsRequestExecCommand;
                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        requestsCounter = 0;
                        lpsRequestExecCommand = new LPSRequest.ExecuteCommand() { LPSTestCaseExecuteCommand = command };
                        for (int i = 0; i < this.Duration.Value && !cancellationToken.IsCancellationRequested; i += this.CoolDownTime.Value)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSRequest, cancellationToken));
                                requestsCounter++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        break;
                    case IterationMode.CRB:
                        requestsCounter = 0;
                        lpsRequestExecCommand = new LPSRequest.ExecuteCommand() { LPSTestCaseExecuteCommand = command };
                        for (int i = 0; i < this.RequestCount.Value && !cancellationToken.IsCancellationRequested; i += this.BatchSize.Value)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSRequest, cancellationToken));
                                requestsCounter++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        break;
                    case IterationMode.CB:
                        requestsCounter = 0;
                        lpsRequestExecCommand = new LPSRequest.ExecuteCommand() { LPSTestCaseExecuteCommand = command };
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSRequest, cancellationToken));
                                requestsCounter++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        break;
                    case IterationMode.R:
                        requestsCounter = 0;
                        lpsRequestExecCommand = new LPSRequest.ExecuteCommand() { LPSTestCaseExecuteCommand = command };
                        for (int i = 0; i < this.RequestCount && !cancellationToken.IsCancellationRequested; i++)
                        {
                            awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSRequest, cancellationToken));
                            requestsCounter++;
                        }
                        break;
                    default:
                        throw new ArgumentException("Invalid IterationMode specified");
                }
                _ = ReportAsync(command, this.LPSRequest.URL, requestsCounter);
                await Task.WhenAll(awaitableTasks);
                Console.WriteLine($"All requests has been processed by {this.LPSRequest.URL} with {command.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {command.NumberOfFailedToCompleteRequests} failed to complete requests");


            }
        }
        private static async Task ReportAsync(ExecuteCommand dto, string url, int requestCount)
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            while ((dto.NumberOfSuccessfullyCompletedRequests + dto.NumberOfFailedToCompleteRequests) != requestCount)
            {
                Console.WriteLine($"    Host: {url}, Successfully completed: {dto.NumberOfSuccessfullyCompletedRequests}, Faile to complete:{dto.NumberOfFailedToCompleteRequests}");
                await Task.Delay(5000);
            }
        }
    }
}
