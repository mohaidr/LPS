using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IMetricsQueryService
    {
        ValueTask<List<IMetricCollector>> GetAsync(Func<IMetricCollector, bool> predicate, CancellationToken token);
        ValueTask<List<T>> GetAsync<T>(Func<T, bool> predicate, CancellationToken token) where T : IMetricCollector;
        Task<List<T>> GetAsync<T>(Func<T, Task<bool>> predicate, CancellationToken token) where T : IMetricCollector;

    }
}
