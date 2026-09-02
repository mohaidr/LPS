#nullable enable
using LPS.Infrastructure.Monitoring.Windowed;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// In-memory, thread-safe historical store for windowed metric snapshots grouped by IterationId.
    /// Stores time-series data for each iteration's windowed metrics.
    /// </summary>
    public interface IHistoricalWindowedMetricDataStore
    {
        ValueTask PushAsync(Guid iterationId, WindowedIterationSnapshot snapshot, CancellationToken token = default);
        bool TryGet(Guid iterationId, out IReadOnlyList<WindowedIterationSnapshot> snapshots);
        bool TryGetLatest(Guid iterationId, out WindowedIterationSnapshot? snapshot);
        IEnumerable<Guid> IterationIds { get; }
        bool Remove(Guid iterationId);
        void Clear();
        int GetCount(Guid iterationId);
    }
}
