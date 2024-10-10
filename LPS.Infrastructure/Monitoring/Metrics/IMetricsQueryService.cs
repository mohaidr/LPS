using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public interface IMetricsQueryService
    {
        Task<List<IMetricCollector>> GetAsync(Func<IMetricCollector, bool> predicate);
        Task<List<T>> GetAsync<T>(Func<T, bool> predicate) where T : IMetricCollector;
    }
}
