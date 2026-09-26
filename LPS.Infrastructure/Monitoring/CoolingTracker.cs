using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;

namespace LPS.Infrastructure.Monitoring;

public sealed class CoolingTracker : ICoolingTracker
{
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Iteration, string Host, string Source), Entry> _entries = new();

    public CoolingTracker(TimeProvider clock = null) => _clock = clock ?? TimeProvider.System;

    public IDisposable BeginBatchCooldown(Guid iterationId, string hostName)
    {
        lock (_gate)
        {
            var entry = GetEntry(iterationId, hostName, "BatchCooldown");
            if (entry.Active++ == 0) entry.Start = _clock.GetUtcNow().UtcDateTime;
            return new Scope(() =>
            {
                lock (_gate)
                {
                    if (--entry.Active == 0)
                        entry.Periods.Add(new CoolingPeriod("BatchCooldown", entry.Start, _clock.GetUtcNow().UtcDateTime));
                }
            });
        }
    }

    public void SetWatchdogState(string hostName, ResourceState state)
    {
        lock (_gate)
        {
            var entry = GetEntry(Guid.Empty, hostName, "Watchdog");
            var cooling = state is ResourceState.Hot or ResourceState.Cooling;
            if (cooling && entry.Active == 0)
            {
                entry.Start = _clock.GetUtcNow().UtcDateTime;
                entry.Active = 1;
            }
            else if (!cooling && entry.Active != 0)
            {
                entry.Periods.Add(new CoolingPeriod("Watchdog", entry.Start, _clock.GetUtcNow().UtcDateTime));
                entry.Active = 0;
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
                    periods.Add(new CoolingPeriod(pair.Key.Source, pair.Value.Start < start ? start : pair.Value.Start, end));
            }

            var result = new List<CoolingPeriod>();
            foreach (var period in periods.OrderBy(period => period.Source).ThenBy(period => period.Start))
            {
                var previous = result.LastOrDefault();
                if (previous != null && previous.Source == period.Source && period.Start <= previous.End)
                    result[^1] = previous with { End = period.End > previous.End ? period.End : previous.End };
                else result.Add(period);
            }
            return result;
        }
    }

    private Entry GetEntry(Guid iterationId, string hostName, string source)
    {
        var key = (iterationId, NormalizeHostName(hostName), source);
        if (!_entries.TryGetValue(key, out var entry)) _entries[key] = entry = new Entry();
        return entry;
    }

    private static string NormalizeHostName(string hostName) =>
        Uri.TryCreate($"http://{hostName}", UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : (hostName ?? string.Empty).ToLowerInvariant();

    private sealed class Entry
    {
        public int Active;
        public DateTime Start;
        public List<CoolingPeriod> Periods { get; } = new();
    }

    private sealed class Scope : IDisposable
    {
        private Action _end;
        public Scope(Action end) => _end = end;
        public void Dispose() => Interlocked.Exchange(ref _end, null)?.Invoke();
    }
}