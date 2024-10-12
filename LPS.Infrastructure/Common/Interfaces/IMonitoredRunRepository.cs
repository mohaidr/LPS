using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using System.Collections.Concurrent;


namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IMonitoredRunRepository
    {
        ConcurrentDictionary<HttpRun, MonitoredHttpRun> MonitoredRuns { get; }
    }
}
