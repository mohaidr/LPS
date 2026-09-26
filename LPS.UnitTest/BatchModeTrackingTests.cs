using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.LPSRequest.LPSHttpRequest;
using LPS.Domain.LPSRun.IterationMode;
using Moq;

namespace LPS.UnitTest;

public class BatchModeTrackingTests
{
    [Theory]
    [InlineData(IterationMode.CB, false)]
    [InlineData(IterationMode.CB, true)]
    [InlineData(IterationMode.CRB, false)]
    [InlineData(IterationMode.CRB, true)]
    [InlineData(IterationMode.DCB, false)]
    [InlineData(IterationMode.DCB, true)]
    public async Task Execute_PreservesOverlappingBatchesAndRequestTotals(IterationMode mode, bool maximizeThroughput)
    {
        var logger = Mock.Of<ILogger>();
        var operationId = Mock.Of<IRuntimeOperationIdProvider>();
        var expressions = Mock.Of<IExpressionEvaluator>();
        var watchdog = Mock.Of<IWatchdog>();
        var iteration = new HttpIteration(new HttpIteration.SetupCommand
        {
            Name = "Batch tracking",
            Mode = mode,
            RequestCount = mode == IterationMode.CRB ? 5 : null,
            Duration = mode == IterationMode.DCB ? 60 : null,
            CoolDownTime = 1,
            BatchSize = 3,
            MaximizeThroughput = maximizeThroughput
        }, expressions, logger, operationId);
        iteration.SetHttpRequest(new HttpRequest(new HttpRequest.SetupCommand
        {
            Url = new URL("https://example.com/"),
            HttpMethod = "GET"
        }, logger, operationId));

        var command = new HttpRequest.ExecuteCommand(
            Mock.Of<IClientService<HttpRequest, HttpResponse>>(),
            Mock.Of<ISkippedRequestReporter>(), logger, watchdog, operationId, expressions);
        var batches = new List<(TaskCompletionSource<int> Completion, int Size)>();
        var bothStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new TestBatchProcessor(size =>
        {
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            batches.Add((completion, size));
            if (batches.Count == 2)
                bothStarted.TrySetResult(true);
            return completion.Task;
        });
        var statuses = new Mock<IIterationStatusMonitor>();
        statuses.Setup(value => value.IsTerminatedAsync(iteration, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(batches.Count >= 2));

        IIterationModeService service = mode switch
        {
            IterationMode.CB => new CBMode(command, 1, 3, maximizeThroughput, processor, iteration, statuses.Object, watchdog),
            IterationMode.CRB => new CRBMode(command, 5, 1, 3, maximizeThroughput, processor, iteration, statuses.Object, watchdog),
            _ => new DCBMode(command, 60, 1, 3, maximizeThroughput, processor, iteration, statuses.Object, watchdog)
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var execution = service.ExecuteAsync(cancellation.Token);
        try
        {
            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(execution.IsCompleted);
            Assert.All(batches, batch => Assert.False(batch.Completion.Task.IsCompleted));
            Assert.Equal(mode == IterationMode.CRB ? new[] { 3, 2 } : new[] { 3, 3 },
                batches.Select(batch => batch.Size));
        }
        finally
        {
            foreach (var batch in batches)
                batch.Completion.TrySetResult(batch.Size);
            Assert.Equal(mode == IterationMode.CRB ? 5 : 6, await execution);
        }

    }

    private sealed class TestBatchProcessor(Func<int, Task<int>> sendBatch)
        : IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest>
    {
        public Task<int> SendBatchAsync(HttpRequest.ExecuteCommand command, int batchSize,
            Func<bool> batchCondition, CancellationToken token) => sendBatch(batchSize);
    }
}
