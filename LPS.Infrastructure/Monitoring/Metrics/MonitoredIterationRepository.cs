using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using System.Collections.Concurrent;


namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class MonitoredIterationRepository : IMonitoredIterationRepository
    {
        public ConcurrentDictionary<HttpIteration, MonitoredHttpIteration> MonitoredIterations { get; } = new ConcurrentDictionary<HttpIteration, MonitoredHttpIteration>();
    }
}
