using LPS.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public interface IMonitoredRunRepository
    {
        ConcurrentDictionary<HttpRun, MonitoredHttpRun> MonitoredRuns { get; }
    }
}
