using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Queue for windowed metrics snapshots. Aggregators push completed windows,
    /// pushers consume and send to external destinations.
    /// </summary>
    public interface IWindowedMetricsQueue
    {
        /// <summary>
        /// Enqueue a completed window snapshot.
        /// </summary>
        bool TryEnqueue(WindowedIterationSnapshot snapshot);

        /// <summary>
        /// Async enqueue with backpressure support.
        /// </summary>
        ValueTask EnqueueAsync(WindowedIterationSnapshot snapshot, CancellationToken token = default);

        /// <summary>
        /// Dequeue the next available snapshot. Blocks until available or cancelled.
        /// </summary>
        ValueTask<WindowedIterationSnapshot> DequeueAsync(CancellationToken token = default);

        /// <summary>
        /// Try to dequeue without blocking.
        /// </summary>
        bool TryDequeue(out WindowedIterationSnapshot snapshot);

        /// <summary>
        /// Get the channel reader for advanced consumption patterns.
        /// </summary>
        ChannelReader<WindowedIterationSnapshot> Reader { get; }

        /// <summary>
        /// Number of items currently in the queue.
        /// </summary>
        int Count { get; }
    }
}
