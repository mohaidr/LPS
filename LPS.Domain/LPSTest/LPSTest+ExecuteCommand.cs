using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTest
    {
        public class ExecuteCommand : IAsyncCommand<LPSTest>
        {
            async public Task ExecuteAsync(LPSTest entity)
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
                foreach (var wrapper in this.LPSRequestWrappers)
                {
                    LPSRequestWrapper.ExecuteCommand wrapperExecutecommand = new LPSRequestWrapper.ExecuteCommand() { LPSTestExecuteCommand = dto };

                    numberOfTestRequests += wrapper.NumberofAsyncRepeats;
                    string eventId = $"{this.Name}.{(this.IsRedo ? "redo." : string.Empty)}{(String.IsNullOrEmpty(wrapper.Name) ? string.Empty : wrapper.Name + ".")}{randomGuidId}";



                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "Request Details", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Number Of Async Calls:  {wrapper.NumberofAsyncRepeats}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Client Http Request Timeout: {wrapper.LPSRequest.HttpRequestTimeout}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Method:  {wrapper.LPSRequest.HttpMethod.ToUpper()}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Version: {wrapper.LPSRequest.Httpversion}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"URL: {wrapper.LPSRequest.URL}", LoggingLevel.INF));
                    if (!string.IsNullOrEmpty(wrapper.LPSRequest.Payload) && (wrapper.LPSRequest.HttpMethod.ToUpper() == "PUT" || wrapper.LPSRequest.HttpMethod.ToUpper() == "POST" || wrapper.LPSRequest.HttpMethod.ToUpper() == "PATCH"))
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Body...", LoggingLevel.INF));
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, wrapper.LPSRequest.Payload, LoggingLevel.INF));
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Body...", LoggingLevel.INF));
                    }
                    else
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Empty Payload...", LoggingLevel.INF));
                    }

                    if (wrapper.LPSRequest.HttpHeaders != null)
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Headers...", LoggingLevel.INF));

                        foreach (var header in wrapper.LPSRequest.HttpHeaders)
                        {
                            awaitableTasks.Add(_logger.LogAsync(string.Empty, $"{header.Key}: {header.Value}", LoggingLevel.INF));
                        }

                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Headers...", LoggingLevel.INF));
                    }
                    else
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...No Headers Were Provided...", LoggingLevel.INF));
                    }

                    awaitableTasks.Add(wrapperExecutecommand.ExecuteAsync(wrapper));
                }

                while (dto.NumberOfSentRequests != numberOfTestRequests)
                {
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
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

