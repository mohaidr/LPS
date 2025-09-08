using System;
using System.Collections.Generic;
using LPS.Domain;

namespace LPS.Infrastructure.Common.Interfaces
{
    /// <summary>
    /// Creates and caches metric aggregators per iteration (one set per iteration).
    /// Lifetime (create/get/remove/clear) is handled here. Start/Stop is the monitor's job.
    /// </summary>
    public interface IMetricAggregatorFactory
    {
        // Create once or return existing (thread-safe).
        IReadOnlyList<IMetricAggregator> GetOrCreate(HttpIteration iteration, string roundName);

        // Lookup
        bool TryGet(Guid iterationId, out IReadOnlyList<IMetricAggregator> aggregators);

        // Remove (optionally dispose IDisposables)
        bool Remove(Guid iterationId, bool dispose = true);

        // Snapshot for monitor-side filtering
        IEnumerable<HttpIteration> Iterations { get; }

        // Maintenance
        void Clear(bool dispose = true);
    }
}
