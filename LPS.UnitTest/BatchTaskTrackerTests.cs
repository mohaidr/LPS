using System.Diagnostics;
using LPS.Domain.LPSRun.IterationMode;

namespace LPS.UnitTest;

public class BatchTaskTrackerTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("fault")]
    [InlineData("cancel")]
    public async Task Cleanup_RemovesCompletedBatchesButKeepsRunningBatches(string outcome)
    {
        var tracker = new BatchTaskTracker();
        var running = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Batch failed");
        tracker.Add(running.Task);
        tracker.Add(outcome switch
        {
            "fault" => Task.FromException<int>(failure),
            "cancel" => Task.FromCanceled<int>(new CancellationToken(true)),
            _ => Task.FromResult(3)
        });

        try
        {
            // Exercise the real five-second timer, not a manually invoked cleanup.
            var timeout = Stopwatch.StartNew();
            while (tracker.PendingBatchCount != 1 && timeout.Elapsed < TimeSpan.FromSeconds(15))
                await Task.Delay(25);

            Assert.Equal(1, tracker.PendingBatchCount);
            Assert.False(running.Task.IsCompleted);
            tracker.Add(Task.FromResult(7));
        }
        finally
        {
            running.TrySetResult(2);
            var completion = tracker.CompleteAsync();
            if (outcome == "fault")
                Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => completion));
            else if (outcome == "cancel")
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
            else
                Assert.Equal(12, await completion);

            Assert.Equal(0, tracker.PendingBatchCount);
        }
    }

    [Fact]
    public async Task Complete_WaitsForRunningBatchesAndCountsEachBatchOnce()
    {
        var tracker = new BatchTaskTracker();
        var batch = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        tracker.Add(batch.Task);
        tracker.Add(Task.FromResult(3));

        var completion = tracker.CompleteAsync();
        Assert.False(completion.IsCompleted);
        Assert.True(tracker.PendingBatchCount > 0);
        batch.SetResult(2);

        Assert.Equal(5, await completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, tracker.PendingBatchCount);
    }

    [Fact]
    public async Task Complete_DrainsBatchesBeforeRethrowingExecutionError()
    {
        var tracker = new BatchTaskTracker();
        var batch = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellation = new OperationCanceledException("Scheduling cancelled");
        tracker.Add(batch.Task);
        tracker.Add(Task.FromException<int>(new InvalidOperationException("Batch failed")));

        var completion = tracker.CompleteAsync(cancellation);
        Assert.False(completion.IsCompleted);
        batch.SetResult(1);

        Assert.Same(cancellation, await Assert.ThrowsAsync<OperationCanceledException>(() => completion));
        Assert.Equal(0, tracker.PendingBatchCount);
    }

    [Fact]
    public async Task Complete_PrioritizesBatchFaultOverBatchCancellation()
    {
        var tracker = new BatchTaskTracker();
        var failure = new InvalidOperationException("Batch failed");
        tracker.Add(Task.FromCanceled<int>(new CancellationToken(true)));
        tracker.Add(Task.FromException<int>(failure));

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => tracker.CompleteAsync()));
        Assert.Equal(0, tracker.PendingBatchCount);
    }

    [Fact]
    public async Task Complete_PreservesRequestCountOverflowDetection()
    {
        var tracker = new BatchTaskTracker();
        tracker.Add(Task.FromResult(int.MaxValue));
        tracker.Add(Task.FromResult(1));

        await Assert.ThrowsAsync<OverflowException>(() => tracker.CompleteAsync());
        Assert.Equal(0, tracker.PendingBatchCount);
    }

    [Fact]
    public async Task ConcurrentAdd_DoesNotLoseBatches()
    {
        var tracker = new BatchTaskTracker();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var index = 0; index < 1000; index++)
                tracker.Add(Task.FromResult(1));
        })));

        Assert.Equal(8000, await tracker.CompleteAsync());
        Assert.Equal(0, tracker.PendingBatchCount);
    }
}
