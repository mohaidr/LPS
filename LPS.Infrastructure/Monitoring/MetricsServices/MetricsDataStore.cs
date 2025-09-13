using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Entity;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Default in-memory implementation for IMetricDataStore.
    /// Keeps a per-iteration dictionary of queues (one queue per metric type).
    /// Thread-safe, lock-free hot path with ConcurrentDictionary + ConcurrentQueue.
    /// </summary>
    public sealed class MetricDataStoreService : IMetricDataStore
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly IEntityRepositoryService _entityRepositoryService;
        // To avoid unbounded memory, you can cap each metric-type queue length.
        private readonly int _perTypeCapacity;

        // Allowed metric types (must match your 4 aggregators).
        private static readonly HashSet<LPSMetricType> _allowedTypes = new()
        {
            LPSMetricType.ResponseCode,
            LPSMetricType.ResponseTime,
            LPSMetricType.Throughput,
            LPSMetricType.DataTransmission
        };
        private sealed class PerType
        {
            public readonly ConcurrentQueue<HttpMetricSnapshot> Queue = new();
            private HttpMetricSnapshot? _latest; // atomic “last item”
            public HttpMetricSnapshot? Latest => Volatile.Read(ref _latest);
            public void SetLatest(HttpMetricSnapshot s) => Volatile.Write(ref _latest, s);
            public int Count; // approximate, maintained with Interlocked
        }

        private sealed class Entry
        {
            public HttpIteration Iteration { get; }
            // One record per metric type
            public ConcurrentDictionary<LPSMetricType, PerType> Types { get; } = new();
            public Entry(HttpIteration iteration) => Iteration = iteration;
        }

        // IterationId -> Entry
        private readonly ConcurrentDictionary<Guid, Entry> _store = new();

        public MetricDataStoreService(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            int perTypeCapacity = 2048) // sensible default; adjust if you want
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _perTypeCapacity = perTypeCapacity > 0 ? perTypeCapacity : throw new ArgumentOutOfRangeException(nameof(perTypeCapacity));
        }

        public async ValueTask PushAsync(HttpIteration iteration, HttpMetricSnapshot snapshot, CancellationToken token = default)
        {
            if (iteration is null) throw new ArgumentNullException(nameof(iteration));
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));

            if (!_allowedTypes.Contains(snapshot.MetricType))
            {
                await _logger.LogAsync(_op.OperationId,
                    $"MetricDataStore: Rejected snapshot for unsupported type '{snapshot.MetricType}' (Iteration {iteration.Id}).",
                    LPSLoggingLevel.Warning, token);
                return;
            }

            var entry = _store.GetOrAdd(iteration.Id, _ => new Entry(iteration));
            var per = entry.Types.GetOrAdd(snapshot.MetricType, _ => new PerType());

            // Push to history
            per.Queue.Enqueue(snapshot);
            Interlocked.Increment(ref per.Count);

            // Publish latest (O(1), lock-free)
            per.SetLatest(snapshot);

            // Best-effort bound without O(n) q.Count
            while (Volatile.Read(ref per.Count) > _perTypeCapacity && per.Queue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref per.Count);
            }
        }

        public bool TryGet(Guid iterationId, LPSMetricType metricType, out IReadOnlyList<HttpMetricSnapshot> snapshots)
        {
            snapshots = Array.Empty<HttpMetricSnapshot>();

            if (!_store.TryGetValue(iterationId, out var entry)) return false;
            if (!entry.Types.TryGetValue(metricType, out var per)) return false;

            snapshots = per.Queue.ToArray(); // OK: not on hot path
            return true;
        }

        public bool TryGetLatest<TSnapshot>(Guid iterationId, LPSMetricType metricType, out TSnapshot? snapshot)
            where TSnapshot : HttpMetricSnapshot
        {
            snapshot = null;

            if (!_store.TryGetValue(iterationId, out var entry)) return false;
            if (!entry.Types.TryGetValue(metricType, out var per)) return false;

            var last = per.Latest;
            if (last is TSnapshot typed)
            {
                snapshot = typed;
                return true;
            }
            return false;
        }

        public IEnumerable<HttpIteration> Iterations =>
            _store.Values.Select(e => e.Iteration);

        public bool Remove(Guid iterationId) =>
            _store.TryRemove(iterationId, out _);

        public void Clear() => _store.Clear();
    }
}
