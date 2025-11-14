using LPS.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IDurationMetricCollector : IMetricAggregator
    {
        public Task<IDurationMetricCollector> UpdateTotalTimeAsync(double totalTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateDownStreamTimeAsync(double downStreamTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateUpStreamTimeAsync(double upStreamTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateTLSHandshakeTimeAsync(double tlsHandshakeTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateTCPHandshakeTimeAsync(double tcpHandshakeTime, CancellationToken token);
    }
}
