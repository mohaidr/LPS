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
    public class MonitoredHttpIteration
    {
        public HttpIteration HttpIteration { get; }
        public ConcurrentBag<string> ExecutionIds { get; }
        public IReadOnlyDictionary<string, IMetricCollector> Metrics { get; }

        public MonitoredHttpIteration(HttpIteration httpIteration, IReadOnlyDictionary<string, IMetricCollector> metrics)
        {
            HttpIteration = httpIteration;
            ExecutionIds = [];
            Metrics = metrics;
        }
    }
}
