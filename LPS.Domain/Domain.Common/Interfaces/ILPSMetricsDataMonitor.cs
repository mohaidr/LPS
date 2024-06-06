using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface ILPSMetricsDataMonitor
    {
        public bool TryRegister(LPSHttpRun lpsHttpRun);
        public void Monitor(LPSHttpRun lpsHttpRun, string executionId);
        public void Stop(LPSHttpRun lpsHttpRun, string executionId);
    }
}
