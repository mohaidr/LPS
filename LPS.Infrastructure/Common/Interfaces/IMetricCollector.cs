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
    public interface IMetricCollector
    {
        public HttpIteration HttpIteration { get; }
        public LPSMetricType MetricType { get; }
        public string Stringify();
        public ValueTask<IDimensionSet> GetDimensionSetAsync(CancellationToken token);
        ValueTask<TDimensionSet> GetDimensionSetAsync<TDimensionSet>(CancellationToken token) where TDimensionSet : IDimensionSet;
        public void Start();
        public void Stop();
        public bool IsStarted { get; }
    }
}