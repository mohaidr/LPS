using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface IMetricsDataMonitor
    {
        public bool TryRegister(HttpRun lpsHttpRun);
        public void Monitor(HttpRun lpsHttpRun, string executionId);
        public void Stop(HttpRun lpsHttpRun, string executionId);
    }
}
