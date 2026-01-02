#nullable enable
using System.Threading.Channels;

namespace LPS.Infrastructure.Monitoring.Cumulative
{
    /// <summary>
    /// Queue for cumulative metric snapshots. 
    /// Separate from windowed queue to allow different push intervals.
    /// </summary>
    public interface ICumulativeMetricsQueue
    {
        bool TryEnqueue(CumulativeIterationSnapshot snapshot);
        ChannelReader<CumulativeIterationSnapshot> Reader { get; }
    }

    public sealed class CumulativeMetricsQueue : ICumulativeMetricsQueue
    {
        private readonly Channel<CumulativeIterationSnapshot> _channel;

        public CumulativeMetricsQueue(int capacity = 1000)
        {
            // TODO: Enhance to stop queuing on worker nodes. 
            // Currently coordinators run on all nodes (master + workers), but only master consumes queues.
            // Workers enqueue snapshots that are never consumed, relying on DropOldest to limit to 1000.
            // Should gate coordinator registration by NodeType.Master to prevent unnecessary queuing on workers.
            _channel = Channel.CreateBounded<CumulativeIterationSnapshot>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                });
        }

        public bool TryEnqueue(CumulativeIterationSnapshot snapshot)
            => _channel.Writer.TryWrite(snapshot);

        public ChannelReader<CumulativeIterationSnapshot> Reader => _channel.Reader;
    }
}
