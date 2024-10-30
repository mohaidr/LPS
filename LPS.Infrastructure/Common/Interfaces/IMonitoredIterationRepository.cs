using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using System.Collections.Concurrent;


namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IMonitoredIterationRepository
    {
        ConcurrentDictionary<HttpIteration, MonitoredHttpIteration> MonitoredIterations { get; }
    }
}
