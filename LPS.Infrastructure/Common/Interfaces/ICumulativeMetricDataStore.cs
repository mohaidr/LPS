#nullable enable
using LPS.Infrastructure.Monitoring.Cumulative;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// In-memory, thread-safe store for cumulative metric snapshots grouped by IterationId.
    /// Stores time-series data for each iteration's cumulative metrics (for persistence).
    /// Similar to IWindowedMetricDataStore but for cumulative snapshots.
    /// </summary>
    public interface ICumulativeMetricDataStore
    {
        /// <summary>Push a new cumulative snapshot for an iteration.</summary>
        ValueTask PushAsync(Guid iterationId, CumulativeIterationSnapshot snapshot, CancellationToken token = default);

        /// <summary>Try to get all cumulative snapshots for a given iteration (FIFO order).</summary>
        bool TryGet(Guid iterationId, out IReadOnlyList<CumulativeIterationSnapshot> snapshots);

        /// <summary>Try to get the latest cumulative snapshot for a given iteration.</summary>
        bool TryGetLatest(Guid iterationId, out CumulativeIterationSnapshot? snapshot);

        /// <summary>Enumerate the iteration IDs that have cumulative data.</summary>
        IEnumerable<Guid> IterationIds { get; }

        /// <summary>Remove an iteration and all its cumulative snapshots.</summary>
        bool Remove(Guid iterationId);

        /// <summary>Clear the entire store.</summary>
        void Clear();

        /// <summary>Get count of snapshots for an iteration.</summary>
        int GetCount(Guid iterationId);
    }
}
