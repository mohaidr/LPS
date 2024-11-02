using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface IMetricsDataMonitor
    {
        public bool TryRegister(string roundName, HttpIteration lpsHttpRun);
        public void Monitor(HttpIteration lpsHttpRun, string executionId);
        public void Stop(HttpIteration lpsHttpRun, string executionId);
    }
}
