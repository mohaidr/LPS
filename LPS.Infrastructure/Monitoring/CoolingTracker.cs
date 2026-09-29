using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;

namespace LPS.Infrastructure.Monitoring;

public sealed class CoolingTracker : ICoolingTracker
{
    private readonly TimeProvider _clock;
    private readonly string _nodeId;
    private readonly string _machineName;
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Iteration, string Host, string Source), Entry> _entries = new();
    private readonly Dictionary<(Guid Iteration, string Host, string Source, string Node, DateTime Start, string Reason), (CoolingUpdate Update, DateTime Received)> _remote = new();
    private bool _completed;

    public CoolingTracker(TimeProvider clock = null, string nodeId = null, string machineName = null)
    {
        _clock = clock ?? TimeProvider.System;
        _nodeId = nodeId ?? INode.NodeIP;
        _machineName = machineName ?? Environment.MachineName;
    }

    public IDisposable BeginBatchCooldown(Guid iterationId, string hostName, string reason = "Configured batch cooldown")
    {
        lock (_gate)
        {
            if (_completed) return new Scope(() => { });
            var entry = GetEntry(iterationId, hostName, "BatchCooldown");
            if (entry.Active++ == 0)
            {
                entry.Start = _clock.GetUtcNow().UtcDateTime;
                entry.Reason = reason;
            }
            return new Scope(() =>
            {
                lock (_gate)
                {
                    if (_completed) return;
                    if (--entry.Active == 0)
                        entry.Periods.Add(CreatePeriod("BatchCooldown", entry, _clock.GetUtcNow().UtcDateTime));
                }
            });
        }
    }

    public void SetWatchdogState(string hostName, ResourceState state, string reason = "Resource pressure")
    {
        lock (_gate)
        {
            if (_completed) return;
            var entry = GetEntry(Guid.Empty, hostName, "Watchdog");
            var cooling = state is ResourceState.Hot or ResourceState.Cooling;
            if (entry.Active != 0 && (!cooling || entry.Reason != reason))
            {
                entry.Periods.Add(CreatePeriod("Watchdog", entry, _clock.GetUtcNow().UtcDateTime));
                entry.Active = 0;
            }
            if (cooling && entry.Active == 0)
            {
                entry.Start = _clock.GetUtcNow().UtcDateTime;
                entry.Reason = reason;
                entry.Active = 1;
            }
        }
    }

    public IReadOnlyList<CoolingPeriod> GetPeriods(string hostName, DateTime start, DateTime end, Guid? iterationId = null)
    {
        hostName = NormalizeHostName(hostName);
        lock (_gate)
        {
            var periods = new List<CoolingPeriod>();
            foreach (var pair in _entries)
            {
                if (!string.Equals(pair.Key.Host, hostName, StringComparison.OrdinalIgnoreCase)) continue;
                if (pair.Key.Source == "BatchCooldown" && iterationId.HasValue && pair.Key.Iteration != iterationId) continue;
                foreach (var period in pair.Value.Periods)
                {
                    if (period.Start < end && period.End > start)
                        periods.Add(period with { Start = period.Start < start ? start : period.Start, End = period.End > end ? end : period.End });
                }
                if (pair.Value.Active > 0 && pair.Value.Start < end)
                    periods.Add(CreatePeriod(pair.Key.Source, pair.Value, end) with { Start = pair.Value.Start < start ? start : pair.Value.Start });
            }

            var result = new List<CoolingPeriod>();
            foreach (var remote in _remote.Values)
            {
                var update = remote.Update;
                if (NormalizeHostName(update.HostName) != hostName) continue;
                if (update.Period.Source == "BatchCooldown" && iterationId.HasValue && update.IterationId != iterationId) continue;
                var period = update.Period;
                if (period.Start < end && remote.Received >= start && remote.Received < end)
                    periods.Add(period with { End = period.End > end ? end : period.End });
                else if (period.Start < end && period.End > start)
                    periods.Add(period with { Start = period.Start < start ? start : period.Start, End = period.End > end ? end : period.End });
            }
            foreach (var period in periods.OrderBy(period => period.Source).ThenBy(period => period.NodeId).ThenBy(period => period.Reason).ThenBy(period => period.Start))
            {
                var previous = result.LastOrDefault();
                if (previous != null && previous.Source == period.Source && previous.NodeId == period.NodeId && previous.Reason == period.Reason && period.Start <= previous.End)
                    result[^1] = previous with { End = period.End > previous.End ? period.End : previous.End };
                else result.Add(period);
            }
            return result;
        }
    }

    public IReadOnlyList<CoolingUpdate> GetUpdates(DateTime since, DateTime until)
    {
        lock (_gate)
        {
            var updates = new List<CoolingUpdate>();
            foreach (var pair in _entries)
            {
                foreach (var period in pair.Value.Periods.Where(period => period.End >= since))
                    updates.Add(new CoolingUpdate(pair.Key.Iteration, pair.Key.Host, period, false));
                if (pair.Value.Active > 0)
                    updates.Add(new CoolingUpdate(pair.Key.Iteration, pair.Key.Host, CreatePeriod(pair.Key.Source, pair.Value, until), true));
            }
            return updates;
        }
    }

    public void ApplyUpdate(CoolingUpdate update)
    {
        var period = update.Period;
        var key = (update.IterationId, NormalizeHostName(update.HostName), period.Source, period.NodeId, period.Start, period.Reason);
        lock (_gate)
        {
            if (_remote.TryGetValue(key, out var previous) &&
                (previous.Update.Period.End > period.End || (!previous.Update.IsActive && update.IsActive))) return;
            _remote[key] = (update, _clock.GetUtcNow().UtcDateTime);
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            if (_completed) return;
            _completed = true;
            var end = _clock.GetUtcNow().UtcDateTime;
            foreach (var pair in _entries.Where(pair => pair.Value.Active > 0))
            {
                pair.Value.Periods.Add(CreatePeriod(pair.Key.Source, pair.Value, end));
                pair.Value.Active = 0;
            }
        }
    }

    private Entry GetEntry(Guid iterationId, string hostName, string source)
    {
        var key = (iterationId, NormalizeHostName(hostName), source);
        if (!_entries.TryGetValue(key, out var entry)) _entries[key] = entry = new Entry();
        return entry;
    }

    private CoolingPeriod CreatePeriod(string source, Entry entry, DateTime end) =>
        new(source, entry.Start, end, _nodeId, _machineName, entry.Reason);

    private static string NormalizeHostName(string hostName) =>
        Uri.TryCreate($"http://{hostName}", UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : (hostName ?? string.Empty).ToLowerInvariant();

    private sealed class Entry
    {
        public int Active;
        public DateTime Start;
        public string Reason;
        public List<CoolingPeriod> Periods { get; } = new();
    }

    private sealed class Scope : IDisposable
    {
        private Action _end;
        public Scope(Action end) => _end = end;
        public void Dispose() => Interlocked.Exchange(ref _end, null)?.Invoke();
    }
}