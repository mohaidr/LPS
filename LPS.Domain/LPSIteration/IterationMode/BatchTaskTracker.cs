using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal sealed class BatchTaskTracker
    {
        private readonly ConcurrentDictionary<long, Task<int>> _batches = new();
        private readonly CancellationTokenSource _cleanupCancellation = new();
        private readonly Task _cleanupTask;
        private long _nextBatchId;
        private long _sentRequests;
        private ExceptionDispatchInfo _failure;
        private ExceptionDispatchInfo _cancellation;

        internal int PendingBatchCount => _batches.Count;

        public BatchTaskTracker()
        {
            _cleanupTask = CleanupAsync();
        }

        public void Add(Task<int> batch)
        {
            ArgumentNullException.ThrowIfNull(batch);
            _batches.TryAdd(Interlocked.Increment(ref _nextBatchId), batch);
        }

        private async Task CleanupAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            try
            {
                while (await timer.WaitForNextTickAsync(_cleanupCancellation.Token).ConfigureAwait(false))
                {
                    foreach (var batch in _batches)
                    {
                        if (batch.Value.IsCompleted && _batches.TryRemove(batch.Key, out var completed))
                            await CollectAsync(completed).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (_cleanupCancellation.IsCancellationRequested)
            {
                // Completion stops the timer before taking ownership of the remaining batches.
            }
        }

        private async Task CollectAsync(Task<int> batch)
        {
            try
            {
                _sentRequests += await batch.ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (batch.IsCanceled)
            {
                _cancellation ??= ExceptionDispatchInfo.Capture(ex);
            }
            catch (Exception ex)
            {
                // Observe every batch, retaining the first fault for propagation after draining.
                _failure ??= ExceptionDispatchInfo.Capture(ex);
            }
        }

        // The producer must stop adding batches before calling this method.
        public async Task<int> CompleteAsync(Exception executionError = null)
        {
            try
            {
                _cleanupCancellation.Cancel();
                await _cleanupTask.ConfigureAwait(false);
                foreach (var batch in _batches)
                {
                    await CollectAsync(batch.Value).ConfigureAwait(false);
                    _batches.TryRemove(batch.Key, out _);
                }
            }
            finally
            {
                _cleanupCancellation.Dispose();
            }

            if (executionError != null)
                ExceptionDispatchInfo.Capture(executionError).Throw();

            _failure?.Throw();
            _cancellation?.Throw();
            return checked((int)_sentRequests);
        }
    }
}
