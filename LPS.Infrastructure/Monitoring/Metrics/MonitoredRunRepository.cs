using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using System.Collections.Concurrent;


namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class MonitoredRunRepository : IMonitoredRunRepository
    {
        public ConcurrentDictionary<HttpRun, MonitoredHttpRun> MonitoredRuns { get; } = new ConcurrentDictionary<HttpRun, MonitoredHttpRun>();
    }
}
