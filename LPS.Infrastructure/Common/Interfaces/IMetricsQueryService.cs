using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    public interface IMetricsQueryService
    {
        ValueTask<List<IMetricAggregator>> GetAsync(Func<IMetricAggregator, bool> predicate, CancellationToken token);
        ValueTask<List<T>> GetAsync<T>(Func<T, bool> predicate, CancellationToken token) where T : IMetricAggregator;
        ValueTask<List<T>> GetAsync<T>(Func<T, Task<bool>> predicate, CancellationToken token) where T : IMetricAggregator;

    }
}
