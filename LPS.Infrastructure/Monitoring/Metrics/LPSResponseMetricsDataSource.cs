using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{

    internal static class LPSResponseMetricsDataSource
    {
        private static List<ILPSResponseMetric> _responseMetric = new List<ILPSResponseMetric>();
        internal static void Register(LPSHttpRun lpsHttpRun)
        {
            if (!_responseMetric.Any(metric => metric.LPSHttpRun == lpsHttpRun))
            {
                _responseMetric.Add(new LPSResponseBreakDownMetric(lpsHttpRun));
                _responseMetric.Add(new LPSDurationMetric(lpsHttpRun));
            }
        }
        public static List<ILPSResponseMetric> Get(Func<ILPSResponseMetric, bool> predicate)
        {
            return _responseMetric.Where(metric => predicate(metric)).ToList();
        }
    }

}
