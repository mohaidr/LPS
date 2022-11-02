using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncTest
    {
        public class ExecuteCommand : ICommand<HttpAsyncTest>
        {
            public void Execute(HttpAsyncTest entity)
            {
                throw new NotImplementedException();
            }

            async public Task ExecuteAsync(HttpAsyncTest entity)
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
                HttpAsyncRequestContainer.ExecuteCommand command = new HttpAsyncRequestContainer.ExecuteCommand() { HttpAsyncTestExecuteCommand = dto };
                foreach (var container in this.HttpRequestContainers)
                {

                    numberOfTestRequests += container.NumberofAsyncRepeats;
                    string eventId = $"{this.Name}.{(this.IsRedo ? "redo." : string.Empty)}{(String.IsNullOrEmpty(container.Name) ? string.Empty : container.Name + ".")}{randomGuidId}";



                    awaitableTasks.Add(_logger.LogAsync(string.Empty, "Request Details", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Number Of Async Calls:  {container.NumberofAsyncRepeats}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Client Http Request Timeout: {container.HttpRequest.HttpRequestTimeout}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Method:  {container.HttpRequest.HttpMethod.ToUpper()}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"Http Version: {container.HttpRequest.Httpversion}", LoggingLevel.INF));
                    awaitableTasks.Add(_logger.LogAsync(string.Empty, $"URL: {container.HttpRequest.URL}", LoggingLevel.INF));
                    if (!string.IsNullOrEmpty(container.HttpRequest.Payload) && (container.HttpRequest.HttpMethod.ToUpper() == "PUT" || container.HttpRequest.HttpMethod.ToUpper() == "POST" || container.HttpRequest.HttpMethod.ToUpper() == "PATCH"))
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Body...", LoggingLevel.INF));
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, container.HttpRequest.Payload, LoggingLevel.INF));
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Body...", LoggingLevel.INF));
                    }
                    else
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Empty Payload...", LoggingLevel.INF));
                    }

                    if (container.HttpRequest.HttpHeaders != null)
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...Begin Request Headers...", LoggingLevel.INF));

                        foreach (var header in container.HttpRequest.HttpHeaders)
                        {
                            awaitableTasks.Add(_logger.LogAsync(string.Empty, $"{header.Key}: {header.Value}", LoggingLevel.INF));
                        }

                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...End Request Headers...", LoggingLevel.INF));
                    }
                    else
                    {
                        awaitableTasks.Add(_logger.LogAsync(string.Empty, "...No Headers Were Provided...", LoggingLevel.INF));
                    }

                    awaitableTasks.Add(command.ExecuteAsync(container));
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

