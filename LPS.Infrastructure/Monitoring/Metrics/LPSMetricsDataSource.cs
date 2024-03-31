using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{

    public static class LPSMetricsDataSource
    {
        private static ConcurrentDictionary<string, ILPSMetric> _metrics = new ConcurrentDictionary<string, ILPSMetric>();

        internal static void Register(LPSHttpRun lpsHttpRun)
        {
            string breakDownMetricKey = $"{lpsHttpRun.Id}-BreakDown";
            string durationMetricKey = $"{lpsHttpRun.Id}-Duration";
            string connectionsMetricKey = $"{lpsHttpRun.Id}-Connections";

            _metrics.TryAdd(breakDownMetricKey, new LPSResponseCodeMetricGroup(lpsHttpRun));
            _metrics.TryAdd(durationMetricKey, new LPSDurationMetric(lpsHttpRun));
            _metrics.TryAdd(connectionsMetricKey, new LPSConnectionsMetricGroup(lpsHttpRun));
        }

        public static List<ILPSMetric> Get(Func<ILPSMetric, bool> predicate)
        {
            // Filter the metrics based on the given predicate.
            return _metrics.Values.Where(predicate).ToList();
        }
    }
}
