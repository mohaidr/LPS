using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRequest.LPSHttpRequest;
using Moq;

namespace LPS.UnitTest
{
    public class RoundExecutionTests : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ILogger _logger = Mock.Of<ILogger>();
        private readonly IRuntimeOperationIdProvider _operationId = Mock.Of<IRuntimeOperationIdProvider>();
        private readonly IMetricsDataMonitor _metrics = Mock.Of<IMetricsDataMonitor>();
        private readonly Mock<IExpressionEvaluator> _expressions = new();
        private readonly Mock<IIterationStatusMonitor> _statuses = new();
        private readonly Mock<ICommandRepository<HttpIteration, IAsyncCommand<HttpIteration>>> _repository = new();
        private readonly Mock<IClientManager<HttpRequest, HttpResponse, IClientService<HttpRequest, HttpResponse>>> _clients = new();
        private readonly Queue<IClientService<HttpRequest, HttpResponse>> _clientQueue = new();
        private readonly List<HttpIteration.ExecuteCommand> _registered = new();
        private readonly ConcurrentQueue<(string SessionId, string Path)> _requests = new();
        private readonly HttpResponse _response;
        private Func<string, HttpRequest, CancellationToken, Task<HttpResponse>> _sendAsync;
        private int _clientCount;

        public RoundExecutionTests()
        {
            _response = new HttpResponse(new HttpResponse.SetupCommand
            {
                StatusCode = HttpStatusCode.OK,
                IsSuccessStatusCode = true
            }, _logger, _operationId);
            _sendAsync = (sessionId, request, token) => Task.FromResult(_response);
            _repository.Setup(repository => repository.Add(It.IsAny<HttpIteration>(), It.IsAny<IAsyncCommand<HttpIteration>>()))
                .Callback<HttpIteration, IAsyncCommand<HttpIteration>>((iteration, command) =>
                    _registered.Add((HttpIteration.ExecuteCommand)command));
            _clients.Setup(clients => clients.CreateInstance(It.IsAny<IClientConfiguration<HttpRequest>>()))
                .Returns(CreateClient);
            _clients.Setup(clients => clients.CreateAndQueueClient(It.IsAny<IClientConfiguration<HttpRequest>>()))
                .Callback(() => _clientQueue.Enqueue(CreateClient()));
            _clients.Setup(clients => clients.DequeueClient())
                .Returns(() => _clientQueue.Count > 0 ? _clientQueue.Dequeue() : null);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Schedule_FlatLoadUsesOnlyClientArrivalSettings(bool sequential, bool emptyStages)
        {
            var round = CreateRound(new Round.SetupCommand
            {
                NumberOfClients = 4,
                ArrivalDelay = 70,
                StartupDelay = 10000,
                RunClientsSequentially = sequential,
                Stages = emptyStages ? Array.Empty<Stage>() : null
            });

            var schedule = RoundScheduleBuilder.Build(round).ToArray();

            Assert.Equal(new long[] { 0, sequential ? 0 : 70, sequential ? 0 : 140, sequential ? 0 : 210 },
                schedule.Select(client => client.ArrivalOffsetMs));
            Assert.Equal(new[] { 0, 1, 2, 3 }, schedule.Select(client => client.ClientIndex));
            Assert.All(schedule, client =>
            {
                Assert.Equal(0, client.StageIndex);
                Assert.Equal(0, client.StartupDelayMs);
            });
            Assert.Equal(schedule, RoundScheduleBuilder.Build(round).ToArray());
            _clients.VerifyNoOtherCalls();
            Assert.Empty(_registered);
            Assert.Empty(_requests);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Schedule_StagesSeparateConcurrentOffsetsFromSequentialPauses(bool sequential)
        {
            var round = CreateRound(new Round.SetupCommand
            {
                NumberOfClients = 99,
                ArrivalDelay = 60000,
                RunClientsSequentially = sequential,
                Stages = new[] { new Stage(2, 40, 30), new Stage(1, 60000, 80), new Stage(2, 60, 25) }
            });

            var schedule = RoundScheduleBuilder.Build(round).ToArray();

            Assert.Equal(new[] { 0, 0, 1, 2, 2 }, schedule.Select(client => client.StageIndex));
            Assert.Equal(new[] { 0, 1, 0, 0, 1 }, schedule.Select(client => client.ClientIndex));
            Assert.Equal(sequential ? new long[5] : new long[] { 30, 70, 150, 175, 235 },
                schedule.Select(client => client.ArrivalOffsetMs));
            Assert.Equal(sequential ? new[] { 30, 0, 80, 25, 0 } : new int[5],
                schedule.Select(client => client.StartupDelayMs));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Schedule_UsesWideOffsetsWithoutAllocatingClients(bool staged)
        {
            var round = CreateRound(new Round.SetupCommand
            {
                NumberOfClients = 3,
                ArrivalDelay = int.MaxValue,
                Stages = staged ? new[] { new Stage(2, int.MaxValue, int.MaxValue), new Stage(1, 0, int.MaxValue) } : null
            });

            var schedule = RoundScheduleBuilder.Build(round).ToArray();
            var firstOffset = staged ? (long)int.MaxValue : 0;

            Assert.Equal(new[] { firstOffset, firstOffset + int.MaxValue, firstOffset + 2L * int.MaxValue },
                schedule.Select(client => client.ArrivalOffsetMs));
            _clients.VerifyNoOtherCalls();
            Assert.Empty(_registered);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task ExecuteAsync_RegistersEveryClientBeforeFirstRequest(bool sequential, bool staged)
        {
            var countsAtExecution = new List<int>();
            _sendAsync = (sessionId, request, token) =>
            {
                countsAtExecution.Add(_registered.Count);
                return Task.FromResult(_response);
            };
            var round = CreateRound(new Round.SetupCommand
            {
                RunClientsSequentially = sequential,
                DelayClientCreationUntilIsNeeded = sequential,
                Stages = staged ? new[] { new Stage(1, 0, 0), new Stage(2, 0, 0) } : null
            }, iterationCount: 2);

            await RunAsync(round).WaitAsync(TimeSpan.FromSeconds(3));

            Assert.Equal(6, _registered.Count);
            Assert.Equal(6, countsAtExecution.Count);
            Assert.All(countsAtExecution, count => Assert.Equal(6, count));
            Assert.Equal(3, _registered.Select(command => command.HttpClientService.SessionId).Distinct().Count());
            _clients.Verify(clients => clients.CreateAndQueueClient(It.IsAny<IClientConfiguration<HttpRequest>>()),
                Times.Exactly(sequential ? 0 : 3));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExecuteAsync_SequentialClientsPreserveIterationParallelism(bool parallelIterations)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _sendAsync = async (sessionId, request, token) =>
            {
                if (sessionId == "client-1")
                    await release.Task.WaitAsync(token);
                return _response;
            };
            var round = CreateRound(new Round.SetupCommand
            {
                RunClientsSequentially = true,
                RunInParallel = parallelIterations,
                ArrivalDelay = 60000
            }, iterationCount: 2);

            var execution = RunAsync(round);

            Assert.Equal(parallelIterations ? 2 : 1, _requests.Count);
            Assert.All(_requests, request => Assert.Equal("client-1", request.SessionId));
            release.SetResult();
            await execution.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.Equal(new[] { "client-1", "client-1", "client-2", "client-2", "client-3", "client-3" },
                _requests.Select(request => request.SessionId).ToArray());
            Assert.All(_registered, command => Assert.Equal(CommandExecutionStatus.Completed, command.Status));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExecuteAsync_DefaultAndExplicitFalseKeepClientsConcurrent(bool explicitlyDisabled)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _sendAsync = async (sessionId, request, token) =>
            {
                await release.Task.WaitAsync(token);
                return _response;
            };
            var setup = new Round.SetupCommand();
            if (explicitlyDisabled)
                setup.RunClientsSequentially = false;

            var execution = RunAsync(CreateRound(setup));

            Assert.Equal(3, _requests.Count);
            Assert.Equal(3, _requests.Select(request => request.SessionId).Distinct().Count());
            release.SetResult();
            await execution.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task ExecuteAsync_SchedulerFaultCancelsUnstartedCommands()
        {
            var failure = new InvalidOperationException("Status monitor failed");
            _statuses.Setup(statuses => statuses.IsTerminatedAsync(It.IsAny<HttpIteration>(), It.IsAny<CancellationToken>()))
                .Returns(() => _registered.Any(command => command.Status == CommandExecutionStatus.Completed)
                    ? Task.FromException<bool>(failure) : Task.FromResult(false));
            var round = CreateRound(new Round.SetupCommand { RunClientsSequentially = true });

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(round));

            Assert.Same(failure, actual);
            Assert.Single(_requests);
            Assert.Equal(3, _registered.Count);
            Assert.All(_registered.Skip(1), command => Assert.Equal(CommandExecutionStatus.Cancelled, command.Status));
        }

        [Fact]
        public async Task ExecuteAsync_CancellationDuringStageDelayCancelsUnstartedCommands()
        {
            var round = CreateRound(new Round.SetupCommand
            {
                RunClientsSequentially = true,
                Stages = new[] { new Stage(3, 0, 60000) }
            });

            var execution = RunAsync(round);
            _cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.WaitAsync(TimeSpan.FromSeconds(3)));

            Assert.Empty(_requests);
            Assert.Equal(3, _registered.Count);
            Assert.All(_registered, command => Assert.Equal(CommandExecutionStatus.Cancelled, command.Status));
        }

        [Fact]
        public async Task ExecuteAsync_SequentialStageDelayStartsAfterPreviousClientsAfterHook()
        {
            var stopwatch = new Stopwatch();
            var starts = new Dictionary<string, long>();
            long afterHookCompletedAt = 0;
            _sendAsync = (sessionId, request, token) =>
            {
                starts.Add(sessionId, stopwatch.ElapsedMilliseconds);
                return Task.FromResult(_response);
            };
            _expressions.Setup(expressions => expressions.RunAsync("after-1", "client-1", It.IsAny<CancellationToken>()))
                .Returns<string, string, CancellationToken>(async (expression, sessionId, token) =>
                {
                    await Task.Delay(150, token);
                    afterHookCompletedAt = stopwatch.ElapsedMilliseconds;
                });
            var round = CreateRound(new Round.SetupCommand
            {
                RunClientsSequentially = true,
                StartupDelay = 30,
                Stages = new[] { new Stage(1, 60000, 40), new Stage(2, 60000, 100) }
            }, iterationStartupDelay: 20);

            stopwatch.Start();
            await RunAsync(round).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(starts["client-1"] >= 80);
            Assert.True(starts["client-2"] - afterHookCompletedAt >= 110);
            Assert.Equal(new[] { "client-1", "client-2", "client-3" },
                _requests.Select(request => request.SessionId).ToArray());
            _expressions.Verify(expressions => expressions.RunAsync("after-1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExecuteAsync_ConcurrentArrivalTimelineDoesNotWaitForEarlierClients(bool staged)
        {
            var stopwatch = new Stopwatch();
            var starts = new ConcurrentDictionary<string, long>();
            var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _sendAsync = async (sessionId, request, token) =>
            {
                starts[sessionId] = stopwatch.ElapsedMilliseconds;
                if (starts.Count == 3)
                    allStarted.TrySetResult();
                await release.Task.WaitAsync(token);
                return _response;
            };
            var round = CreateRound(new Round.SetupCommand
            {
                ArrivalDelay = 50,
                Stages = staged ? new[] { new Stage(2, 40, 30), new Stage(1, 0, 80) } : null
            });

            stopwatch.Start();
            var execution = RunAsync(round);
            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.False(execution.IsCompleted);
            Assert.True(starts["client-1"] >= (staged ? 25 : 0));
            Assert.True(starts["client-2"] >= (staged ? 65 : 45));
            Assert.True(starts["client-3"] >= (staged ? 145 : 95));
            release.SetResult();
            await execution.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task ExecuteAsync_ConcurrentArrivalTimelineStartsAfterPreparation(bool staged, bool delayClientCreation)
        {
            var stopwatch = Stopwatch.StartNew();
            var starts = new ConcurrentDictionary<string, long>();
            long preparationCompletedAt = 0;
            _repository.Setup(repository => repository.Add(It.IsAny<HttpIteration>(), It.IsAny<IAsyncCommand<HttpIteration>>()))
                .Callback<HttpIteration, IAsyncCommand<HttpIteration>>((iteration, command) =>
                {
                    _registered.Add((HttpIteration.ExecuteCommand)command);
                    if (_registered.Count == 3)
                    {
                        Task.Delay(350).GetAwaiter().GetResult();
                        preparationCompletedAt = stopwatch.ElapsedMilliseconds;
                    }
                });
            _sendAsync = (sessionId, request, token) =>
            {
                starts[sessionId] = stopwatch.ElapsedMilliseconds - preparationCompletedAt;
                return Task.FromResult(_response);
            };
            var round = CreateRound(new Round.SetupCommand
            {
                ArrivalDelay = 100,
                DelayClientCreationUntilIsNeeded = delayClientCreation,
                Stages = staged ? new[] { new Stage(2, 100, 80), new Stage(1, 0, 150) } : null
            });

            await RunAsync(round).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(3, starts.Count);
            Assert.True(starts["client-1"] >= (staged ? 70 : 0));
            Assert.True(starts["client-2"] >= (staged ? 170 : 90));
            Assert.True(starts["client-3"] >= (staged ? 320 : 190));
            Assert.All(_registered, command => Assert.Equal(CommandExecutionStatus.Completed, command.Status));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExecuteAsync_CancellationDuringRequestsDoesNotStartLaterClients(bool parallelIterations)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _sendAsync = async (sessionId, request, token) =>
            {
                await release.Task.WaitAsync(token);
                return _response;
            };
            var round = CreateRound(new Round.SetupCommand
            {
                RunClientsSequentially = true,
                RunInParallel = parallelIterations
            }, iterationCount: 2);

            var execution = RunAsync(round);
            Assert.Equal(parallelIterations ? 2 : 1, _requests.Count);
            _cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.WaitAsync(TimeSpan.FromSeconds(3)));

            Assert.All(_requests, request => Assert.Equal("client-1", request.SessionId));
            Assert.Equal(6, _registered.Count);
            Assert.All(_registered, command => Assert.Equal(CommandExecutionStatus.Cancelled, command.Status));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExecuteAsync_OrdinaryRequestFailureOrSkipDoesNotBlockLaterClients(bool skipFirstClient)
        {
            if (skipFirstClient)
            {
                _expressions.Setup(expressions => expressions.EvaluateAsync(It.IsAny<string>(), "client-1", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);
            }
            else
            {
                _sendAsync = (sessionId, request, token) => sessionId == "client-1"
                    ? Task.FromException<HttpResponse>(new HttpRequestException("Request failed"))
                    : Task.FromResult(_response);
            }
            var round = CreateRound(new Round.SetupCommand { RunClientsSequentially = true });

            await RunAsync(round).WaitAsync(TimeSpan.FromSeconds(3));

            Assert.Equal(skipFirstClient ? 2 : 3, _requests.Count);
            Assert.Contains(_requests, request => request.SessionId == "client-2");
            Assert.Contains(_requests, request => request.SessionId == "client-3");
            Assert.Equal(skipFirstClient ? CommandExecutionStatus.Skipped : CommandExecutionStatus.Completed, _registered[0].Status);
            Assert.All(_registered.Skip(1), command => Assert.Equal(CommandExecutionStatus.Completed, command.Status));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExecuteAsync_TerminatedIterationDoesNotBlockOtherIterationsOrClients(bool parallelIterations)
        {
            _statuses.Setup(statuses => statuses.IsTerminatedAsync(It.IsAny<HttpIteration>(), It.IsAny<CancellationToken>()))
                .Returns<HttpIteration, CancellationToken>((iteration, token) => Task.FromResult(iteration.Name == "Iteration-1"));
            var round = CreateRound(new Round.SetupCommand
            {
                RunClientsSequentially = true,
                RunInParallel = parallelIterations
            }, iterationCount: 2);

            await RunAsync(round).WaitAsync(TimeSpan.FromSeconds(3));

            Assert.Equal(3, _requests.Count);
            Assert.All(_requests, request => Assert.EndsWith("/iteration-2", request.Path));
            Assert.Equal(3, _requests.Select(request => request.SessionId).Distinct().Count());
        }

        [Fact]
        public async Task ExecuteAsync_PreparationFailureCancelsPartiallyRegisteredClients()
        {
            var failure = new InvalidOperationException("Registration failed");
            _repository.Setup(repository => repository.Add(It.IsAny<HttpIteration>(), It.IsAny<IAsyncCommand<HttpIteration>>()))
                .Callback<HttpIteration, IAsyncCommand<HttpIteration>>((iteration, command) =>
                {
                    _registered.Add((HttpIteration.ExecuteCommand)command);
                    if (_registered.Count == 3)
                        throw failure;
                });
            var round = CreateRound(new Round.SetupCommand
            {
                RunClientsSequentially = true,
                DelayClientCreationUntilIsNeeded = true
            }, iterationCount: 2);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(round));

            Assert.Same(failure, actual);
            Assert.Empty(_requests);
            Assert.Equal(3, _registered.Count);
            Assert.All(_registered, command => Assert.Equal(CommandExecutionStatus.Cancelled, command.Status));
        }

        [Theory]
        [InlineData(IterationMode.CRB, false)]
        [InlineData(IterationMode.CRB, true)]
        [InlineData(IterationMode.CB, false)]
        [InlineData(IterationMode.CB, true)]
        [InlineData(IterationMode.DCB, false)]
        [InlineData(IterationMode.DCB, true)]
        public async Task BatchModes_RecordRealCooldownsAndCloseThemOnCancellation(IterationMode mode, bool maximizeThroughput)
        {
            var tracker = new LPS.Infrastructure.Monitoring.CoolingTracker();
            Mock.Get(_metrics).Setup(value => value.BeginBatchCooldown(It.IsAny<HttpIteration>()))
                .Returns<HttpIteration>(iteration => tracker.BeginBatchCooldown(iteration.Id, iteration.HttpRequest.Url.HostName));
            var round = CreateRound(new Round.SetupCommand { NumberOfClients = 1 }, mode: mode, maximizeThroughput: maximizeThroughput);
            var start = DateTime.UtcNow;
            _cancellation.CancelAfter(TimeSpan.FromMilliseconds(250));
            try { await RunAsync(round); }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
            var end = DateTime.UtcNow;

            var periods = tracker.GetPeriods("example.com", start, end);
            Assert.NotEmpty(periods);
            Assert.All(periods, period => Assert.Equal("BatchCooldown", period.Source));
            Assert.All(periods, period => Assert.True(period.End > period.Start));
            Assert.Empty(tracker.GetPeriods("example.com", end, end.AddSeconds(1)));
            Mock.Get(_metrics).Verify(value => value.BeginBatchCooldown(It.IsAny<HttpIteration>()), Times.AtLeastOnce());
        }

        private Round CreateRound(Round.SetupCommand setup, int iterationCount = 1, int iterationStartupDelay = 0,
            IterationMode mode = IterationMode.R, bool maximizeThroughput = false)
        {
            setup.Name ??= "Round";
            setup.NumberOfClients ??= setup.Stages?.Sum(stage => stage.NumberOfClients) ?? 3;
            var round = new Round(setup, _logger, _metrics, _operationId);
            Assert.True(round.IsValid);
            for (var index = 1; index <= iterationCount; index++)
            {
                var iteration = new HttpIteration(new HttpIteration.SetupCommand
                {
                    Name = $"Iteration-{index}",
                    Mode = mode,
                    RequestCount = mode == IterationMode.R ? 1 : mode == IterationMode.CRB ? 2 : null,
                    Duration = mode == IterationMode.DCB ? 1 : null,
                    BatchSize = mode == IterationMode.R ? null : 1,
                    CoolDownTime = mode == IterationMode.R ? null : 20,
                    MaximizeThroughput = maximizeThroughput,
                    StartupDelay = iterationStartupDelay,
                    After = new List<string> { $"after-{index}" }
                }, _expressions.Object, _logger, _operationId);
                iteration.SetHttpRequest(new HttpRequest(new HttpRequest.SetupCommand
                {
                    Url = new URL($"https://example.com/iteration-{index}"),
                    HttpMethod = "GET"
                }, _logger, _operationId));
                round.AddIteration(iteration);
            }
            return round;
        }

        private IClientService<HttpRequest, HttpResponse> CreateClient()
        {
            var sessionId = $"client-{++_clientCount}";
            var client = new Mock<IClientService<HttpRequest, HttpResponse>>();
            client.SetupGet(current => current.SessionId).Returns(sessionId);
            client.Setup(current => current.SendAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                .Returns<HttpRequest, CancellationToken>((request, token) =>
                {
                    _requests.Enqueue((sessionId, request.Url.Url));
                    return _sendAsync(sessionId, request, token);
                });
            return client.Object;
        }

        private Task RunAsync(Round round) => new Round.ExecuteCommand(
            _logger, Mock.Of<IWatchdog>(), _operationId, Mock.Of<ISkippedRequestReporter>(),
            _clients.Object, Mock.Of<IClientConfiguration<HttpRequest>>(), _repository.Object,
            _metrics, _statuses.Object).ExecuteAsync(round, _cancellation.Token);

        public void Dispose()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}