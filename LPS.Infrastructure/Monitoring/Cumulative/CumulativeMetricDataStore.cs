#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.Infrastructure.Monitoring.Cumulative
{
    /// <summary>
    /// In-memory implementation for ICumulativeMetricDataStore.
    /// Keeps a per-iteration queue of cumulative snapshots.
    /// Thread-safe, lock-free with ConcurrentDictionary + ConcurrentQueue.
    /// Bounded to prevent memory overflow during long tests.
    /// Similar to WindowedMetricDataStore but for cumulative snapshots.
    /// </summary>
    public sealed class CumulativeMetricDataStore : ICumulativeMetricDataStore
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly int _capacity;

        private sealed class Entry
        {
            public readonly ConcurrentQueue<CumulativeIterationSnapshot> Queue = new();
            private CumulativeIterationSnapshot? _latest;
            public CumulativeIterationSnapshot? Latest => Volatile.Read(ref _latest);
            public void SetLatest(CumulativeIterationSnapshot s) => Volatile.Write(ref _latest, s);
            public int Count; // approximate, maintained with Interlocked
        }

        private readonly ConcurrentDictionary<Guid, Entry> _store = new();

        public CumulativeMetricDataStore(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            int capacity = 2048)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        public async ValueTask PushAsync(Guid iterationId, CumulativeIterationSnapshot snapshot, CancellationToken token = default)
        {
            if (snapshot is null)
            {
                await _logger.LogAsync(_op.OperationId, "Cumulative snapshot was not provided", LPSLoggingLevel.Error, token);
                throw new ArgumentNullException(nameof(snapshot));
            }

            var entry = _store.GetOrAdd(iterationId, _ => new Entry());

            // Push to history
            entry.Queue.Enqueue(snapshot);
            Interlocked.Increment(ref entry.Count);

            // Publish latest (O(1), lock-free)
            entry.SetLatest(snapshot);

            // Best-effort bound without O(n) q.Count
            // Drop oldest snapshots if capacity exceeded
            while (Volatile.Read(ref entry.Count) > _capacity && entry.Queue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref entry.Count);
            }
        }

        public bool TryGet(Guid iterationId, out IReadOnlyList<CumulativeIterationSnapshot> snapshots)
        {
            snapshots = Array.Empty<CumulativeIterationSnapshot>();

            if (!_store.TryGetValue(iterationId, out var entry)) return false;

            snapshots = entry.Queue.ToArray(); // snapshot the queue
            return snapshots.Count > 0;
        }

        public bool TryGetLatest(Guid iterationId, out CumulativeIterationSnapshot? snapshot)
        {
            snapshot = null;

            if (!_store.TryGetValue(iterationId, out var entry)) return false;

            snapshot = entry.Latest;
            return snapshot is not null;
        }

        public IEnumerable<Guid> IterationIds => _store.Keys;

        public bool Remove(Guid iterationId)
        {
            return _store.TryRemove(iterationId, out _);
        }

        public void Clear()
        {
            _store.Clear();
        }

        public int GetCount(Guid iterationId)
        {
            if (!_store.TryGetValue(iterationId, out var entry)) return 0;
            return Volatile.Read(ref entry.Count);
        }
    }
}
