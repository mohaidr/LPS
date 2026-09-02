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
    /// In-memory historical store for cumulative metric snapshots.
    /// Keeps a bounded, thread-safe snapshot history per iteration.
    /// </summary>
    public sealed class HistoricalCumulativeMetricDataStore : IHistoricalCumulativeMetricDataStore
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly int _capacity;

        private sealed class Entry
        {
            public readonly ConcurrentQueue<CumulativeIterationSnapshot> Queue = new();
            private CumulativeIterationSnapshot? _latest;
            public CumulativeIterationSnapshot? Latest => Volatile.Read(ref _latest);
            public void SetLatest(CumulativeIterationSnapshot snapshot) => Volatile.Write(ref _latest, snapshot);
            public int Count;
        }

        private readonly ConcurrentDictionary<Guid, Entry> _store = new();

        public HistoricalCumulativeMetricDataStore(
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
            entry.Queue.Enqueue(snapshot);
            Interlocked.Increment(ref entry.Count);
            entry.SetLatest(snapshot);

            while (Volatile.Read(ref entry.Count) > _capacity && entry.Queue.TryDequeue(out _))
                Interlocked.Decrement(ref entry.Count);
        }

        public bool TryGet(Guid iterationId, out IReadOnlyList<CumulativeIterationSnapshot> snapshots)
        {
            snapshots = Array.Empty<CumulativeIterationSnapshot>();
            if (!_store.TryGetValue(iterationId, out var entry)) return false;
            snapshots = entry.Queue.ToArray();
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
        public bool Remove(Guid iterationId) => _store.TryRemove(iterationId, out _);
        public void Clear() => _store.Clear();
        public int GetCount(Guid iterationId) =>
            _store.TryGetValue(iterationId, out var entry) ? Volatile.Read(ref entry.Count) : 0;
    }
}
