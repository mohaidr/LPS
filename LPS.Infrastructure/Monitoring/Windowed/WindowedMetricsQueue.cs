using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Windowed
{
    /// <summary>
    /// Thread-safe queue for windowed metrics using System.Threading.Channels.
    /// Aggregators produce, pushers consume.
    /// </summary>
    public sealed class WindowedMetricsQueue : IWindowedMetricsQueue
    {
        private readonly Channel<WindowedIterationSnapshot> _channel;

        public WindowedMetricsQueue(int capacity = 1000)
        {
            // Bounded channel to prevent unbounded memory growth
            _channel = Channel.CreateBounded<WindowedIterationSnapshot>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = false,
                    SingleWriter = false
                });
        }

        public bool TryEnqueue(WindowedIterationSnapshot snapshot)
        {
            return _channel.Writer.TryWrite(snapshot);
        }

        public ValueTask EnqueueAsync(WindowedIterationSnapshot snapshot, CancellationToken token = default)
        {
            return _channel.Writer.WriteAsync(snapshot, token);
        }

        public ValueTask<WindowedIterationSnapshot> DequeueAsync(CancellationToken token = default)
        {
            return _channel.Reader.ReadAsync(token);
        }

        public bool TryDequeue(out WindowedIterationSnapshot snapshot)
        {
            return _channel.Reader.TryRead(out snapshot);
        }

        public ChannelReader<WindowedIterationSnapshot> Reader => _channel.Reader;

        public int Count => _channel.Reader.Count;
    }
}
