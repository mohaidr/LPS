using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IMetricsVariableService
    {
        Task PutMetricAsync(
            string roundName,
            string iterationName,
            string metricName,
            string dimensionSetJson,
            CancellationToken token);
    }

}
