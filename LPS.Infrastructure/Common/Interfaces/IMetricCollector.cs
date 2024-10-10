using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
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
        public HttpRun LPSHttpRun { get; }
        public LPSMetricType MetricType { get; }
        public string Stringify();
        public Task<IDimensionSet> GetDimensionSetAsync();
        Task<TDimensionSet> GetDimensionSetAsync<TDimensionSet>() where TDimensionSet : IDimensionSet;
        public void Start();
        public void Stop();
        public bool IsStopped { get; }
    }
}