using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// In-memory, thread-safe store for metric snapshots grouped by IterationId and MetricType.
    /// Enforces exactly the 4 supported metric types per iteration.
    /// </summary>
    public interface IMetricDataStore
    {
        /// <summary>Push a new snapshot for an iteration + metric type. Registers the iteration on first push.</summary>
        ValueTask PushAsync(HttpIteration iteration, HttpMetricSnapshot snapshot, CancellationToken token = default);

        /// <summary>Try to get all snapshots for a given iteration and metric type (FIFO order).</summary>
        bool TryGet(Guid iterationId, LPSMetricType metricType, out IReadOnlyList<HttpMetricSnapshot> snapshots);

        /// <summary>Try to get the latest snapshot for a given iteration and metric type.</summary>
        bool TryGetLatest<TSnapshot>(Guid iterationId, LPSMetricType metricType, out TSnapshot? snapshot)
            where TSnapshot : HttpMetricSnapshot;

        /// <summary>
        /// Gets the latest snapshot for each metric type for the given iteration (0–4 items),
        /// ordered by LPSMetricType. Returns false if none exist.
        /// </summary>
        bool TryGetLatest(Guid iterationId, out IReadOnlyList<HttpMetricSnapshot> snapshots);


        /// <summary>
        /// Try to get all snapshots for a given iteration across all metric types.
        /// Recommended to return them ordered chronologically if your snapshot type carries a timestamp/sequence.
        /// </summary>
        bool TryGet(Guid iterationId, out IReadOnlyList<HttpMetricSnapshot> snapshots);

        /// <summary>Enumerate the registered iterations (those that have pushed at least one snapshot).</summary>
        IEnumerable<HttpIteration> Iterations { get; }

        /// <summary>Remove an iteration and all its snapshots.</summary>
        bool Remove(Guid iterationId);

        /// <summary>Clear the entire store.</summary>
        void Clear();
    }
}
