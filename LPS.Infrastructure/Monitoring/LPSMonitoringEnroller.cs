using HdrHistogram;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring
{
    public class LPSMonitoringEnroller: ILPSMonitoringEnroller
    {
        public LPSMonitoringEnroller() { }
        public void Enroll(LPSHttpRun lpsHttpRun)
        {

            LPSMetricsDataSource.Register(lpsHttpRun);
        }
    }

}
