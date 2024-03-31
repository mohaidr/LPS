using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Common.Interfaces
{
    public enum LPSMetricType
    {
        ResponseTime,
        ResponseCode,
        ConnectionsCount
    }
    public interface ILPSMetric
    {
        public LPSHttpRun LPSHttpRun { get; }
        public LPSMetricType MetricType { get; }
        public string Stringify();
        public IDimensionSet GetDimensionSet();
    }
}