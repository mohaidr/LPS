#nullable enable
using LPS.Infrastructure.Monitoring.Cumulative;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// In-memory, thread-safe historical store for cumulative metric snapshots grouped by IterationId.
    /// Stores time-series data for each iteration's cumulative metrics for persistence.
    /// </summary>
    public interface IHistoricalCumulativeMetricDataStore
    {
        ValueTask PushAsync(Guid iterationId, CumulativeIterationSnapshot snapshot, CancellationToken token = default);
        bool TryGet(Guid iterationId, out IReadOnlyList<CumulativeIterationSnapshot> snapshots);
        bool TryGetLatest(Guid iterationId, out CumulativeIterationSnapshot? snapshot);
        IEnumerable<Guid> IterationIds { get; }
        bool Remove(Guid iterationId);
        void Clear();
        int GetCount(Guid iterationId);
    }
}
