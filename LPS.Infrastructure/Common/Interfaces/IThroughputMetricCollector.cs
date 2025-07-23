using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IThroughputMetricCollector : IMetricCollector
    {
        public ValueTask<bool> IncreaseConnectionsCount(CancellationToken token);
        public ValueTask<bool> DecreseConnectionsCount(CancellationToken token);
    }
}
