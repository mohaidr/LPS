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
    public class MetricsContainer
    {
        public IReadOnlyList<IMetricAggregator> Metrics { get; }

        public MetricsContainer(IReadOnlyList<IMetricAggregator> metrics)
        {
            Metrics = metrics;
        }
    }
}
