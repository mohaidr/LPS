using LPS.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IDurationMetricCollector : IMetricAggregator
    {
        public Task<IDurationMetricCollector> UpdateTotalTimeAsync(double totalTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateReceivingTimeAsync(double receivingTime, CancellationToken token); // RENAMED from UpdateDownStreamTimeAsync
        public Task<IDurationMetricCollector> UpdateSendingTimeAsync(double sendingTime, CancellationToken token);     // RENAMED from UpdateUpStreamTimeAsync
        public Task<IDurationMetricCollector> UpdateTLSHandshakeTimeAsync(double tlsHandshakeTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateTCPHandshakeTimeAsync(double tcpHandshakeTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateTimeToFirstByteAsync(double timeToFirstByte, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateWaitingTimeAsync(double waitingTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateServerTimeAsync(double serverTime, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateServerTimeDBAsync(double serverTimeDB, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateServerTimeCacheAsync(double serverTimeCache, CancellationToken token);
        public Task<IDurationMetricCollector> UpdateServerTimeAppAsync(double serverTimeApp, CancellationToken token);
    }
}
