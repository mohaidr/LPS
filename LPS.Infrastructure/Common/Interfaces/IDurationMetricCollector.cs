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
        public Task<IDurationMetricCollector> UpdateWaitingTimeAsync(double waitingTime, CancellationToken token); // NEW
    }
}
