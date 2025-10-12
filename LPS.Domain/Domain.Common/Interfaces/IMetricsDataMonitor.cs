using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface IMetricsDataMonitor
    {
        public ValueTask<bool> TryRegisterAsync(string roundName, HttpIteration lpsHttpIteration);
        public ValueTask MonitorAsync(HttpIteration lpsHttpIteration, CancellationToken token);
        public ValueTask MonitorAsync(Func<HttpIteration, bool> predicate, CancellationToken token);
        public ValueTask StopAsync(HttpIteration lpsHttpIteration, CancellationToken token);
        public ValueTask StopAsync(Func<HttpIteration, bool> predicate, CancellationToken token);
    }
}
