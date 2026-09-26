using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring;

namespace LPS.UnitTest;

public sealed class CoolingTrackerTests
{
    [Theory]
    [InlineData("example.com")]
    [InlineData("example.com:443")]
    public void TracksBothSourcesEvenWhenTheirPeriodsOverlap(string hostName)
    {
        var clock = new Clock();
        var tracker = new CoolingTracker(clock);
        var iteration = Guid.NewGuid();
        var start = clock.GetUtcNow().UtcDateTime;
        tracker.SetWatchdogState(hostName, ResourceState.Hot);
        clock.Advance(1);
        using (tracker.BeginBatchCooldown(iteration, hostName))
        {
            clock.Advance(1);
            tracker.SetWatchdogState(hostName, ResourceState.Cooling);
            var active = tracker.GetPeriods("EXAMPLE.COM", start, clock.GetUtcNow().UtcDateTime, iteration);
            Assert.Equal(2, active.Count);
            Assert.Contains(active, period => period.Source == "Watchdog" && period.Start == start);
        }
        clock.Advance(1);
        tracker.SetWatchdogState(hostName, ResourceState.Cool);
        var periods = tracker.GetPeriods("example.com", start, clock.GetUtcNow().UtcDateTime, iteration);
        Assert.Equal(1, periods.Single(period => period.Source == "BatchCooldown").End.Subtract(start).TotalSeconds - 1);
        Assert.Equal(3, periods.Single(period => period.Source == "Watchdog").End.Subtract(start).TotalSeconds);
        Assert.Empty(tracker.GetPeriods("another.com", start, clock.GetUtcNow().UtcDateTime));
    }

    [Fact]
    public void OverlappingClientsCoalesceAndScopesCloseOnlyOnce()
    {
        var clock = new Clock();
        var tracker = new CoolingTracker(clock);
        var iteration = Guid.NewGuid();
        var start = clock.GetUtcNow().UtcDateTime;
        var first = tracker.BeginBatchCooldown(iteration, "example.com");
        clock.Advance(1);
        var second = tracker.BeginBatchCooldown(iteration, "EXAMPLE.COM");
        clock.Advance(1);
        first.Dispose();
        first.Dispose();
        clock.Advance(1);
        second.Dispose();

        var period = Assert.Single(tracker.GetPeriods("example.com", start, clock.GetUtcNow().UtcDateTime, iteration));
        Assert.Equal(start, period.Start);
        Assert.Equal(start.AddSeconds(3), period.End);
        Assert.Empty(tracker.GetPeriods("example.com", start, clock.GetUtcNow().UtcDateTime, Guid.NewGuid()));
        Assert.Empty(tracker.GetPeriods("example.com", start.AddSeconds(3), start.AddSeconds(4)));
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }
}