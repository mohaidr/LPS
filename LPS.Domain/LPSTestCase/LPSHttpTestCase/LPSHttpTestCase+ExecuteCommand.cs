using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            public ExecuteCommand()
            {
                LPSTestPlanExecuteCommand = new LPSTestPlan.ExecuteCommand();
            }

            async public Task ExecuteAsync(LPSHttpTestCase entity, CancellationToken cancellationToken)
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
                awaitableTasks.Add(_logger.LogAsync(string.Empty, "Request Details", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Request Count:  {this.RequestCount}", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Method:  {this.LPSHttpRequest.HttpMethod.ToUpper()}", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Version: {this.LPSHttpRequest.Httpversion}", LoggingLevel.INF));
                awaitableTasks.Add(_logger.LogAsync(string.Empty, $"URL: {this.LPSHttpRequest.URL}", LoggingLevel.INF));
                if (!string.IsNullOrEmpty(this.LPSHttpRequest.Payload)
                    && (this.LPSHttpRequest.HttpMethod.ToUpper() == "PUT"
                    || this.LPSHttpRequest.HttpMethod.ToUpper() == "POST"
                    || this.LPSHttpRequest.HttpMethod.ToUpper() == "PATCH"))
                {
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Body...", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, this.LPSHttpRequest.Payload, LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Body...", LoggingLevel.INF));
                }
                else
                {
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Empty Payload...", LoggingLevel.INF));
                }

                if (this.LPSHttpRequest.HttpHeaders != null)
                {
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Headers...", LoggingLevel.INF));

                    foreach (var header in this.LPSHttpRequest.HttpHeaders)
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

                ILPSClientService<LPSHttpRequest> client;
                bool clientIncorrectlyDelayed = this._httpClient == null && this.Plan.DelayClientCreationUntilIsNeeded.HasValue && !this.Plan.DelayClientCreationUntilIsNeeded.Value;
                if (clientIncorrectlyDelayed)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Warning: The client Was not ready eventhough the delay client creation was set to 'No'. Client will be created now");
                    awaitableTasks.Add(_logger.LogAsync("0000-00000-0000", "The client Was not ready eventhough the delay client creation was set to 'No'", LoggingLevel.WRN));
                    Console.ResetColor();
                }

                if ((this.Plan.DelayClientCreationUntilIsNeeded.HasValue && this.Plan.DelayClientCreationUntilIsNeeded.Value)
                    || clientIncorrectlyDelayed)
                {
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).Timeout = TimeSpan.FromSeconds(this.Plan.ClientTimeout);
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).PooledConnectionLifetime = TimeSpan.FromMinutes(this.Plan.PooledConnectionLifetime);
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).PooledConnectionIdleTimeout = TimeSpan.FromMinutes(this.Plan.PooledConnectionIdleTimeout);
                    ((ILPSHttpClientConfiguration<LPSHttpRequest>)_config).MaxConnectionsPerServer = this.Plan.MaxConnectionsPerServer;
                    client = _lpsClientManager.CreateInstance(_config);
                }
                else
                {
                    client = this._httpClient;
                }

                LPSHttpRequest.ExecuteCommand lpsRequestExecCommand = new LPSHttpRequest.ExecuteCommand() { LPSTestCaseExecuteCommand = command, HttpClientService = client };
                int requestsCounter=0;

                switch (this.Mode)
                {
                    case IterationMode.DCB:
                        for (int i = 0; i < this.Duration.Value && !cancellationToken.IsCancellationRequested; i += this.CoolDownTime.Value)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                                requestsCounter++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        break;
                    case IterationMode.CRB:
                        for (int i = 0; i < this.RequestCount.Value && !cancellationToken.IsCancellationRequested; i += this.BatchSize.Value)
                        {
                            for (int b = 0; b < this.BatchSize; b++)
                            {
                                awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                                requestsCounter++;
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
                                requestsCounter++;
                            }
                            await Task.Delay(this.CoolDownTime.Value, cancellationToken);
                        }
                        break;
                    case IterationMode.R:
                        for (int i = 0; i < this.RequestCount && !cancellationToken.IsCancellationRequested; i++)
                        {
                            awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                            requestsCounter++;
                        }
                        Console.WriteLine("Ready For Reporting");
                        break;
                    case IterationMode.D:
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < this.Duration.Value && !cancellationToken.IsCancellationRequested)
                        {
                            awaitableTasks.Add(lpsRequestExecCommand.ExecuteAsync(LPSHttpRequest, cancellationToken));
                            requestsCounter++;
                        }
                        stopwatch.Stop();
                        break;
                    default:
                        throw new ArgumentException("Invalid iteration mode was chosen");
                }
                _ = ReportAsync(command, this.LPSHttpRequest.URL, requestsCounter, cancellationToken);
                await Task.WhenAll(awaitableTasks);
                Console.WriteLine($"All requests has been processed by {this.LPSHttpRequest.URL} with {command.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {command.NumberOfFailedToCompleteRequests} failed to complete requests");
            }
        }
        private static async Task ReportAsync(ExecuteCommand dto, string url, int requestCount, CancellationToken cancellationToken)
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            while ((dto.NumberOfSuccessfullyCompletedRequests + dto.NumberOfFailedToCompleteRequests) != requestCount)
            {
                Console.WriteLine($"\tHost: {url}, Successfully completed: {dto.NumberOfSuccessfullyCompletedRequests}, Faile to complete:{dto.NumberOfFailedToCompleteRequests}");
                await Task.Delay(5000);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
