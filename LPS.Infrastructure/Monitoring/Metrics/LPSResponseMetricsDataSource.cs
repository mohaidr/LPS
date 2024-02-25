using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{

    public static class LPSResponseMetricsDataSource
    {
        private static List<ILPSResponseMetric> _responseMetric = new List<ILPSResponseMetric>();
        private static readonly object _lock = new object();
        internal static void Register(LPSHttpRun lpsHttpRun)
        {
            lock (_lock)
            {
                if (!_responseMetric.Any(metric => metric.LPSHttpRun.Id == lpsHttpRun.Id))
                {
                    _responseMetric.Add(new LPSResponseBreakDownMetric(lpsHttpRun));
                    _responseMetric.Add(new LPSDurationMetric(lpsHttpRun));
                }
            }
        }
        public static List<ILPSResponseMetric> Get(Func<ILPSResponseMetric, bool> predicate)
        {
            return _responseMetric.Where(metric => predicate(metric)).ToList();
        }
    }

}
