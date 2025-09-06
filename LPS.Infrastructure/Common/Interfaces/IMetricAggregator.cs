using LPS.Domain;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public enum LPSMetricType
    {
        ResponseTime,
        ResponseCode,
        Throughput,
        DataTransmission
    }
    public interface IMetricAggregator
    {
        public HttpIteration HttpIteration { get; }
        public LPSMetricType MetricType { get; }
        public string Stringify();
        public ValueTask<IMetricShapshot> GetSnapshotAsync(CancellationToken token);
        ValueTask<TDimensionSet> GetSnapshotAsync<TDimensionSet>(CancellationToken token) where TDimensionSet : IMetricShapshot;
        public void Start();
        public void Stop();
        public bool IsStarted { get; }
    }
}