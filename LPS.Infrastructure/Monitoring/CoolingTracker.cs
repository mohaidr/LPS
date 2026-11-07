using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Dictionary<string, Entry> _entries = new();
    private readonly Dictionary<(string Host, string Node, DateTime Start, string Reason), (CoolingUpdate Update, DateTime Received)> _remote = new();
    private bool _completed;

    public CoolingTracker(TimeProvider clock = null, string nodeId = null, string machineName = null)
    {
        _clock = clock ?? TimeProvider.System;
        _nodeId = nodeId ?? INode.NodeIP;
        _machineName = machineName ?? Environment.MachineName;
    }

    public void SetWatchdogState(string hostName, ResourceState state, string reason = "Resource pressure")
    {
        lock (_gate)
        {
            if (_completed) return;
            var entry = GetEntry(hostName);
            var cooling = state is ResourceState.Hot or ResourceState.Cooling;
            if (entry.IsCooling && (!cooling || entry.Reason != reason))
            {
                entry.Periods.Add(CreatePeriod(entry, _clock.GetUtcNow().UtcDateTime));
                entry.IsCooling = false;
            }
            if (cooling && !entry.IsCooling)
            {
                entry.Start = _clock.GetUtcNow().UtcDateTime;
                entry.Reason = reason;
                entry.IsCooling = true;
            }
        }
    }

    public IReadOnlyList<CoolingPeriod> GetPeriods(string hostName, DateTime start, DateTime end)
    {
        hostName = NormalizeHostName(hostName);
        lock (_gate)
        {
            var periods = new List<CoolingPeriod>();
            foreach (var pair in _entries)
            {
                if (pair.Key != hostName) continue;
                foreach (var period in pair.Value.Periods)
                {
                    if (period.Start < end && period.End > start)
                        periods.Add(period with { Start = period.Start < start ? start : period.Start, End = period.End > end ? end : period.End });
                }
                if (pair.Value.IsCooling && pair.Value.Start < end)
                    periods.Add(CreatePeriod(pair.Value, end) with { Start = pair.Value.Start < start ? start : pair.Value.Start });
            }

            var result = new List<CoolingPeriod>();
            foreach (var remote in _remote.Values)
            {
                var update = remote.Update;
                if (NormalizeHostName(update.HostName) != hostName) continue;
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
                    updates.Add(new CoolingUpdate(pair.Key, period, false));
                if (pair.Value.IsCooling)
                    updates.Add(new CoolingUpdate(pair.Key, CreatePeriod(pair.Value, until), true));
            }
            return updates;
        }
    }

    public void ApplyUpdate(CoolingUpdate update)
    {
        var period = update.Period;
        if (period.Source != "Watchdog")
            throw new ArgumentException("Only watchdog cooling updates are supported.", nameof(update));
        var key = (NormalizeHostName(update.HostName), period.NodeId, period.Start, period.Reason);
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
            foreach (var pair in _entries.Where(pair => pair.Value.IsCooling))
            {
                pair.Value.Periods.Add(CreatePeriod(pair.Value, end));
                pair.Value.IsCooling = false;
            }
        }
    }

    private Entry GetEntry(string hostName)
    {
        var key = NormalizeHostName(hostName);
        if (!_entries.TryGetValue(key, out var entry)) _entries[key] = entry = new Entry();
        return entry;
    }

    private CoolingPeriod CreatePeriod(Entry entry, DateTime end) =>
        new("Watchdog", entry.Start, end, _nodeId, _machineName, entry.Reason);

    private static string NormalizeHostName(string hostName) =>
        Uri.TryCreate($"http://{hostName}", UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : (hostName ?? string.Empty).ToLowerInvariant();

    private sealed class Entry
    {
        public bool IsCooling;
        public DateTime Start;
        public string Reason;
        public List<CoolingPeriod> Periods { get; } = new();
    }

}