using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class ExecuteCommand : IAsyncCommand<LPSTestPlan>
        {
            async public Task ExecuteAsync(LPSTestPlan entity)
            {
                Reset();
                await entity.ExecuteAsync(this);
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
        async private Task ExecuteAsync(ExecuteCommand dto)
        {
            if (this.IsValid)
            {
                List<Task> awaitableTasks = new List<Task>();

                int numberOfTestRequests = 0;

                var watch = System.Diagnostics.Stopwatch.StartNew();
                var randomGuidId = Guid.NewGuid();
                foreach (var testCase in this.LPSTestCases)
                {
                    LPSTestCase.ExecuteCommand testCaseExecutecommand = new LPSTestCase.ExecuteCommand() { LPSTestPlanExecuteCommand = dto };

                    numberOfTestRequests += testCase.Count.Value;
                    string eventId = $"{this.Name}.{(this.IsRedo ? "redo." : string.Empty)}{(String.IsNullOrEmpty(testCase.Name) ? string.Empty : testCase.Name + ".")}{randomGuidId}";

                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "Request Details", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Request Count:  {testCase.Count}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Client Http Request Timeout: {testCase.LPSRequest.HttpRequestTimeout}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Method:  {testCase.LPSRequest.HttpMethod.ToUpper()}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Version: {testCase.LPSRequest.Httpversion}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"URL: {testCase.LPSRequest.URL}", LoggingLevel.INF));
                    if (!string.IsNullOrEmpty(testCase.LPSRequest.Payload) && (testCase.LPSRequest.HttpMethod.ToUpper() == "PUT" || testCase.LPSRequest.HttpMethod.ToUpper() == "POST" || testCase.LPSRequest.HttpMethod.ToUpper() == "PATCH"))
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Body...", LoggingLevel.INF));
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, testCase.LPSRequest.Payload, LoggingLevel.INF));
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Body...", LoggingLevel.INF));
                    }
                    else
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Empty Payload...", LoggingLevel.INF));
                    }

                    if (testCase.LPSRequest.HttpHeaders != null)
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Headers...", LoggingLevel.INF));

                        foreach (var header in testCase.LPSRequest.HttpHeaders)
                        {
                            awaitableTasks.Add(_logger.LogAsync(string.Empty, $"{header.Key}: {header.Value}", LoggingLevel.INF));
                        }

                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Headers...", LoggingLevel.INF));
                    }
                    else
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...No Headers Were Provided...", LoggingLevel.INF));
                    }

                    awaitableTasks.Add(testCaseExecutecommand.ExecuteAsync(testCase));
                }

                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                while (dto.NumberOfSentRequests != numberOfTestRequests)
                {
                    await Task.Delay(10);
                }
                Thread.CurrentThread.Priority = ThreadPriority.Normal;
                var elapsedMs = watch.ElapsedMilliseconds;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"...All Requests has been sent in {elapsedMs} ms...");
                awaitableTasks.Add(_logger.LogAsync($"{this.Name}.{(this.IsRedo ? "redo." : string.Empty)}{randomGuidId}", $"...All Requests has been sent in {elapsedMs} ms...", LoggingLevel.INF));
                Console.ResetColor();
                await Task.WhenAll(awaitableTasks.ToArray());
                watch.Stop();
                Console.WriteLine("Test ({0}) has completed within {1} ms and results has been written to {2}", this.Name.Length , watch.ElapsedMilliseconds, Path.GetFullPath(Directory.GetCurrentDirectory()));
            }
        }
    }
}

