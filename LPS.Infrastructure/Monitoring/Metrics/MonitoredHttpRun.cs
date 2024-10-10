using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class MonitoredHttpRun
    {
        public HttpRun HttpRun { get; }
        public ConcurrentBag<string> ExecutionIds { get; }
        public IReadOnlyDictionary<string, IMetricCollector> Metrics { get; }

        public MonitoredHttpRun(HttpRun httpRun, IReadOnlyDictionary<string, IMetricCollector> metrics)
        {
            HttpRun = httpRun;
            ExecutionIds = new ConcurrentBag<string>();
            Metrics = metrics;
        }
    }
}
