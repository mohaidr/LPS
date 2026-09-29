using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Monitoring;

namespace LPS.UnitTest;

public sealed class CoolingTrackerTests
{
    [Theory]
    [InlineData(false, "limit")]
    [InlineData(true, "recovery threshold")]
    public void PressureReasonsIncludeAllExceededThresholds(bool recovering, string label)
    {
        var reason = LPS.Infrastructure.Watchdog.Watchdog.DescribePressure(1200, 80, 120, 1000, 70, 100, recovering);
        Assert.Equal($"Memory above 1000 MB {label}; CPU at/above 70% {label}; Target connections above 100 {label}", reason);
        Assert.Equal($"CPU at/above 70% {label}",
            LPS.Infrastructure.Watchdog.Watchdog.DescribePressure(500, 70, 20, 1000, 70, 100, recovering));
    }

    [Theory]
    [InlineData(false, "limit")]
    [InlineData(true, "recovery threshold")]
    public void ConsolePressureReasonsIncludeCurrentReadingsAndThresholds(bool recovering, string label)
    {
        var reason = LPS.Infrastructure.Watchdog.Watchdog.DescribePressure(1200.5, 80.25, 3033,
            1000, 70, 3000, recovering, includeMeasurements: true);

        Assert.Equal($"Memory above 1000 MB {label} (current=1200.5 MB); " +
            $"CPU at/above 70% {label} (current=80.25%); " +
            $"Target connections above 3000 {label} (current=3033)", reason);
        Assert.Equal($"Target connections above 3000 {label} (current=3033)",
            LPS.Infrastructure.Watchdog.Watchdog.DescribePressure(500, 30, 3033,
                1000, 70, 3000, recovering, includeMeasurements: true));
    }

    [Fact]
    public void PreservesMachineAndReasonAcrossWindowClippingAndReasonChanges()
    {
        var clock = new Clock();
        var tracker = new CoolingTracker(clock, "10.0.0.2", "worker-east");
        var start = clock.GetUtcNow().UtcDateTime;
        tracker.SetWatchdogState("example.com", ResourceState.Hot, "CPU above limit");
        clock.Advance(2);
        tracker.SetWatchdogState("example.com", ResourceState.Cooling, "Memory above recovery threshold");
        clock.Advance(2);
        tracker.SetWatchdogState("example.com", ResourceState.Cool);

        var periods = tracker.GetPeriods("example.com", start.AddSeconds(1), start.AddSeconds(3));

        Assert.Equal(2, periods.Count);
        Assert.All(periods, period =>
        {
            Assert.Equal("10.0.0.2", period.NodeId);
            Assert.Equal("worker-east", period.MachineName);
        });
        Assert.Contains(periods, period => period.Reason == "CPU above limit" && period.Start == start.AddSeconds(1) && period.End == start.AddSeconds(2));
        Assert.Contains(periods, period => period.Reason == "Memory above recovery threshold" && period.Start == start.AddSeconds(2) && period.End == start.AddSeconds(3));
    }

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

    [Fact]
    public void RemoteWorkersRemainDistinctAndRetriesCannotReopenCompletedPeriods()
    {
        var clock = new Clock();
        var tracker = new CoolingTracker(clock, "master", "master");
        var start = clock.GetUtcNow().UtcDateTime;
        var iteration = Guid.NewGuid();
        var first = new CoolingUpdate(iteration, "example.com",
            new CoolingPeriod("BatchCooldown", start, start.AddSeconds(1), "worker-1", "East", "CRB batch pause"), true);
        var second = first with { Period = first.Period with { NodeId = "worker-2", MachineName = "West" } };
        tracker.ApplyUpdate(first);
        tracker.ApplyUpdate(second);
        clock.Advance(2);
        tracker.ApplyUpdate(first with { Period = first.Period with { End = start.AddSeconds(2) }, IsActive = false });
        tracker.ApplyUpdate(first);

        var periods = tracker.GetPeriods("example.com", start, start.AddSeconds(3), iteration);

        Assert.Equal(2, periods.Count);
        Assert.Equal(start.AddSeconds(2), periods.Single(period => period.NodeId == "worker-1").End);
        Assert.Equal(start.AddSeconds(1), periods.Single(period => period.NodeId == "worker-2").End);
        Assert.Empty(tracker.GetPeriods("example.com", start, start.AddSeconds(3), Guid.NewGuid()));
        Assert.Empty(tracker.GetUpdates(start, start.AddSeconds(3)));
    }

    [Fact]
    public void RemoteActiveStateNeverExtendsBeyondTheLastWorkerReport()
    {
        var clock = new Clock();
        var tracker = new CoolingTracker(clock);
        var start = clock.GetUtcNow().UtcDateTime;
        tracker.ApplyUpdate(new CoolingUpdate(Guid.Empty, "example.com",
            new CoolingPeriod("Watchdog", start, start.AddSeconds(1), "worker-1", "East", "CPU above limit"), true));
        clock.Advance(6);

        Assert.Equal(start.AddSeconds(1), Assert.Single(tracker.GetPeriods("example.com", start, start.AddSeconds(6))).End);
        Assert.Empty(tracker.GetPeriods("example.com", start.AddSeconds(2), start.AddSeconds(6)));
    }

    [Fact]
    public void LateReportsAppearInTheNextWindowWithTheirActualTimestamps()
    {
        var clock = new Clock();
        var tracker = new CoolingTracker(clock);
        var start = clock.GetUtcNow().UtcDateTime;
        clock.Advance(6);
        tracker.ApplyUpdate(new CoolingUpdate(Guid.Empty, "example.com",
            new CoolingPeriod("Watchdog", start.AddSeconds(3), start.AddSeconds(4), "worker-1", "East", "CPU limit"), false));
        clock.Advance(1);

        var period = Assert.Single(tracker.GetPeriods("example.com", start.AddSeconds(5), start.AddSeconds(7)));
        Assert.Equal(start.AddSeconds(3), period.Start);
        Assert.Equal(start.AddSeconds(4), period.End);
        Assert.Empty(tracker.GetPeriods("example.com", start.AddSeconds(7), start.AddSeconds(8)));
    }

    [Fact]
    public void CompletionClosesActiveSourcesAndPreventsReopening()
    {
        var clock = new Clock();
        var tracker = new CoolingTracker(clock);
        var start = clock.GetUtcNow().UtcDateTime;
        using var scope = tracker.BeginBatchCooldown(Guid.NewGuid(), "example.com", "CB batch pause");
        tracker.SetWatchdogState("example.com", ResourceState.Hot, "CPU above limit");
        clock.Advance(2);
        tracker.Complete();
        tracker.Complete();
        tracker.SetWatchdogState("example.com", ResourceState.Hot);
        clock.Advance(2);

        var updates = tracker.GetUpdates(start, start.AddSeconds(4));
        Assert.Equal(2, updates.Count);
        Assert.All(updates, update =>
        {
            Assert.False(update.IsActive);
            Assert.Equal(start.AddSeconds(2), update.Period.End);
        });
        Assert.Empty(tracker.GetUpdates(start.AddSeconds(3), start.AddSeconds(4)));
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }
}