using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface IMetricsDataMonitor
    {
        public ValueTask<bool> TryRegisterAsync(string roundName, HttpIteration lpsHttpIteration);
        public ValueTask MonitorAsync(HttpIteration lpsHttpIteration);
        public ValueTask MonitorAsync(Func<HttpIteration, bool> predicate);
        public ValueTask StopAsync(HttpIteration lpsHttpIteration);
        public ValueTask StopAsync(Func<HttpIteration, bool> predicate);
    }
}
