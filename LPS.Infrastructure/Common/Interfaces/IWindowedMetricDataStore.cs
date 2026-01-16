using LPS.Domain;
using LPS.Infrastructure.Monitoring.Windowed;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// In-memory, thread-safe store for windowed metric snapshots grouped by IterationId.
    /// Stores time-series data for each iteration's windowed metrics.
    /// </summary>
    public interface IWindowedMetricDataStore
    {
        /// <summary>Push a new windowed snapshot for an iteration.</summary>
        ValueTask PushAsync(Guid iterationId, WindowedIterationSnapshot snapshot, CancellationToken token = default);

        /// <summary>Try to get all windowed snapshots for a given iteration (FIFO order).</summary>
        bool TryGet(Guid iterationId, out IReadOnlyList<WindowedIterationSnapshot> snapshots);

        /// <summary>Try to get the latest windowed snapshot for a given iteration.</summary>
        bool TryGetLatest(Guid iterationId, out WindowedIterationSnapshot? snapshot);

        /// <summary>Enumerate the iteration IDs that have windowed data.</summary>
        IEnumerable<Guid> IterationIds { get; }

        /// <summary>Remove an iteration and all its windowed snapshots.</summary>
        bool Remove(Guid iterationId);

        /// <summary>Clear the entire store.</summary>
        void Clear();

        /// <summary>Get count of snapshots for an iteration.</summary>
        int GetCount(Guid iterationId);
    }
}
