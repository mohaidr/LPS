using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using System;

namespace LPS.Infrastructure.Common.Interfaces
{
    public enum LPSMetricType
    {
        ResponseTime,
        ResponseCode,
        ConnectionsCount
    }
    public interface IMetricMonitor
    {
        public HttpRun LPSHttpRun { get; }
        public LPSMetricType MetricType { get; }
        public string Stringify();
        public IDimensionSet GetDimensionSet();
        TDimensionSet GetDimensionSet<TDimensionSet>() where TDimensionSet : IDimensionSet;
        public void Start();
        public void Stop();
        public bool IsStopped { get; }
    }
}