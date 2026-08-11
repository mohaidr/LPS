#nullable enable
using System.Threading.Channels;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    public interface IHostCumulativeMetricsQueue
    {
        bool TryEnqueue(HostCumulativeMetricsSnapshot snapshot);
        ChannelReader<HostCumulativeMetricsSnapshot> Reader { get; }
    }

    public interface IHostWindowedMetricsQueue
    {
        bool TryEnqueue(HostWindowedMetricsSnapshot snapshot);
        ChannelReader<HostWindowedMetricsSnapshot> Reader { get; }
    }

    public sealed class HostCumulativeMetricsQueue : IHostCumulativeMetricsQueue
    {
        private readonly Channel<HostCumulativeMetricsSnapshot> _channel;

        public HostCumulativeMetricsQueue(int capacity = 1000)
        {
            _channel = Channel.CreateBounded<HostCumulativeMetricsSnapshot>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                });
        }

        public bool TryEnqueue(HostCumulativeMetricsSnapshot snapshot) =>
            _channel.Writer.TryWrite(snapshot);

        public ChannelReader<HostCumulativeMetricsSnapshot> Reader => _channel.Reader;
    }

    public sealed class HostWindowedMetricsQueue : IHostWindowedMetricsQueue
    {
        private readonly Channel<HostWindowedMetricsSnapshot> _channel;

        public HostWindowedMetricsQueue(int capacity = 1000)
        {
            _channel = Channel.CreateBounded<HostWindowedMetricsSnapshot>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false
                });
        }

        public bool TryEnqueue(HostWindowedMetricsSnapshot snapshot) =>
            _channel.Writer.TryWrite(snapshot);

        public ChannelReader<HostWindowedMetricsSnapshot> Reader => _channel.Reader;
    }
}