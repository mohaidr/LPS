using System.Net;
using System.Linq;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Monitoring.Hosts;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;
using Moq;

namespace LPS.UnitTest;

public sealed class HostMetricsAggregatorTests
{
    [Fact]
    public async Task WindowedCollector_PreservesBothCoolingSourcesWithActiveTraffic()
    {
        Func<Task>? close = null;
        var coordinator = new Mock<IWindowedMetricsCoordinator>();
        coordinator.SetupAdd(value => value.OnWindowClosed += It.IsAny<Func<Task>>())
            .Callback<Func<Task>>(handler => close = handler);
        HostWindowedMetricsSnapshot? published = null;
        var queue = new Mock<IHostWindowedMetricsQueue>();
        queue.Setup(value => value.TryEnqueue(It.IsAny<HostWindowedMetricsSnapshot>()))
            .Callback<HostWindowedMetricsSnapshot>(snapshot => published = snapshot).Returns(true);
        var periods = new[]
        {
            new CoolingPeriod("Watchdog", DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow),
            new CoolingPeriod("BatchCooldown", DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow)
        };
        var cooling = new Mock<ICoolingTracker>();
        cooling.Setup(value => value.GetPeriods("example.com", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).Returns(periods);
        using var aggregator = new HostMetricsAggregator(new HostKey("https", "example.com", 443));
        using var collector = new HostWindowedMetricsCollector(aggregator, queue.Object, coordinator.Object, coolingTracker: cooling.Object);
        await aggregator.IncreaseConnectionsCountAsync(CancellationToken.None);

        await close!();

        Assert.Same(periods, published!.CoolingPeriods);
        Assert.Equal(1, published.Throughput.RequestsCount);
    }

    [Fact]
    public async Task Collector_RetainsLatestPublishedHostSnapshotIncludingFinalStatus()
    {
        Func<Task>? push = null;
        var coordinator = new Mock<ICumulativeMetricsCoordinator>();
        coordinator.SetupAdd(value => value.OnPushInterval += It.IsAny<Func<Task>>())
            .Callback<Func<Task>>(handler => push = handler);
        HostCumulativeMetricsSnapshot? published = null;
        var queue = new Mock<IHostCumulativeMetricsQueue>();
        queue.Setup(value => value.TryEnqueue(It.IsAny<HostCumulativeMetricsSnapshot>()))
            .Callback<HostCumulativeMetricsSnapshot>(snapshot => published = snapshot).Returns(true);
        var hostKey = new HostKey("https", "example.com", 443);
        var status = EntityExecutionStatus.Ongoing;
        var tracker = new HostExecutionStatusTracker(null);
        tracker.Track(hostKey, Guid.NewGuid(), () => status);
        using var aggregator = new HostMetricsAggregator(hostKey);
        using var collector = new HostCumulativeMetricsCollector(aggregator, queue.Object, coordinator.Object, tracker);
        Assert.Null(collector.LatestSnapshot);

        await aggregator.IncreaseConnectionsCountAsync(CancellationToken.None);
        await aggregator.UpdateDurationAsync(DurationMetricType.TotalTime, 100, CancellationToken.None);
        await push!();
        var first = collector.LatestSnapshot;
        Assert.Same(published, first);
        Assert.False(first!.IsFinal);

        await aggregator.IncreaseConnectionsCountAsync(CancellationToken.None);
        await aggregator.UpdateDurationAsync(DurationMetricType.TotalTime, 200, CancellationToken.None);
        status = EntityExecutionStatus.Success;
        await push!();

        Assert.Same(published, collector.LatestSnapshot);
        Assert.NotSame(first, collector.LatestSnapshot);
        Assert.True(collector.LatestSnapshot!.IsFinal);
        Assert.Equal("Completed", collector.LatestSnapshot.ExecutionStatus);
        Assert.Equal(2, collector.LatestSnapshot.Throughput.RequestsCount);
        Assert.Equal(150, collector.LatestSnapshot.Duration.TotalTime!.Average);
        Assert.Equal(1, first.Throughput.RequestsCount);
    }

    [Fact]
    public void CumulativeRequestRate_UsesSuccessfulRequestsAcrossIterations()
    {
        using var aggregator = new HostCumulativeThroughputAggregator();

        var cumulative = aggregator.GetCumulativeData(
            elapsedSeconds: 2,
            successful: 20,
            failed: 5);

        Assert.Equal(10, cumulative.RequestsPerSecond);
    }

    [Fact]
    public void HostStatus_IsCompletedOnlyWhenAllTrackedIterationsAreTerminal()
    {
        var firstStatus = EntityExecutionStatus.Success;
        var secondStatus = EntityExecutionStatus.Ongoing;
        var hostKey = new HostKey("https", "example.com", 443);
        var tracker = new HostExecutionStatusTracker(null);
        tracker.Track(hostKey, Guid.NewGuid(), () => firstStatus);
        tracker.Track(hostKey, Guid.NewGuid(), () => secondStatus);

        Assert.Equal(HostExecutionStatus.Ongoing, tracker.GetStatus(hostKey));

        secondStatus = EntityExecutionStatus.Failed;

        Assert.Equal(HostExecutionStatus.Completed, tracker.GetStatus(hostKey));
    }

    [Fact]
    public void Factory_ReusesAggregatorForEquivalentHost()
    {
        using var factory = new HostMetricsAggregatorFactory();

        var first = factory.GetOrCreate(new Uri("HTTPS://EXAMPLE.COM/path"));
        var second = factory.GetOrCreate(new Uri("https://example.com:443/other"));

        Assert.Same(first, second);
        Assert.Equal(new HostKey("https", "example.com", 443), first.HostKey);
    }

    [Fact]
    public async Task Snapshots_ResetWindowWithoutResettingCumulativeData()
    {
        using var factory = new HostMetricsAggregatorFactory();
        var aggregator = factory.GetOrCreate(new Uri("https://example.com"));

        await aggregator.IncreaseConnectionsCountAsync(CancellationToken.None);
        await aggregator.UpdateResponseAsync(new HttpResponse.SetupCommand
        {
            StatusCode = HttpStatusCode.OK,
            StatusMessage = "OK",
            IsSuccessStatusCode = true
        }, CancellationToken.None);
        await aggregator.UpdateDurationAsync(DurationMetricType.TotalTime, 100, CancellationToken.None);
        await aggregator.UpdateDurationAsync(DurationMetricType.TotalTime, 200, CancellationToken.None);
        await aggregator.UpdateDataSentAsync(30, CancellationToken.None);
        await aggregator.UpdateDataReceivedAsync(70, CancellationToken.None);
        await aggregator.DecreaseConnectionsCountAsync(CancellationToken.None);

        var firstWindow = aggregator.GetWindowedSnapshotAndReset(new HostFailureCounts(1, 0));
        var emptyWindow = aggregator.GetWindowedSnapshotAndReset(default);
        var cumulative = aggregator.GetCumulativeSnapshot(new HostFailureCounts(1, 0));

        Assert.Equal(1, firstWindow.Throughput.RequestsCount);
        Assert.Equal(1, firstWindow.Throughput.SuccessfulRequestCount);
        Assert.Equal(2, firstWindow.Duration.TotalTime.Count);
        Assert.Equal(150, firstWindow.Duration.TotalTime.Average);
        Assert.Equal(30, firstWindow.DataTransmission.DataSent);
        Assert.Equal(70, firstWindow.DataTransmission.DataReceived);
        Assert.Single(firstWindow.ResponseCodes.ResponseSummaries);

        Assert.Equal(0, emptyWindow.Throughput.RequestsCount);
        Assert.Equal(0, emptyWindow.Throughput.SuccessfulRequestCount);
        Assert.Equal(0, emptyWindow.Duration.TotalTime.Count);
        Assert.Empty(emptyWindow.ResponseCodes.ResponseSummaries);

        Assert.Equal(1, cumulative.Throughput.RequestsCount);
        Assert.Equal(1, cumulative.Throughput.SuccessfulRequestCount);
        Assert.Equal(150, cumulative.Duration.TotalTime?.Average);
        Assert.Equal(30, cumulative.DataTransmission.DataSent);
        Assert.Equal(70, cumulative.DataTransmission.DataReceived);
        Assert.Single(cumulative.ResponseCodes.ResponseSummaries);
    }

    [Fact]
    public async Task ConcurrentMetricUpdates_ArePreservedAcrossAggregators()
    {
        using var factory = new HostMetricsAggregatorFactory();
        var aggregator = factory.GetOrCreate(new Uri("https://example.com"));
        const int updateCount = 100;

        var updates = Enumerable.Range(0, updateCount).Select(async _ =>
        {
            await aggregator.IncreaseConnectionsCountAsync(CancellationToken.None);
            await aggregator.UpdateResponseAsync(new HttpResponse.SetupCommand
            {
                StatusCode = HttpStatusCode.OK,
                StatusMessage = "OK",
                IsSuccessStatusCode = true
            }, CancellationToken.None);
            await aggregator.UpdateDurationAsync(DurationMetricType.TotalTime, 10, CancellationToken.None);
            await aggregator.UpdateDataSentAsync(20, CancellationToken.None);
            await aggregator.UpdateDataReceivedAsync(30, CancellationToken.None);
            await aggregator.DecreaseConnectionsCountAsync(CancellationToken.None);
        });

        await Task.WhenAll(updates);
        var cumulative = aggregator.GetCumulativeSnapshot(new HostFailureCounts(updateCount, 0));

        Assert.Equal(updateCount, cumulative.Throughput.RequestsCount);
        Assert.Equal(updateCount, cumulative.Throughput.SuccessfulRequestCount);
        Assert.Equal(10, cumulative.Duration.TotalTime?.Average);
        Assert.Equal(updateCount * 20, cumulative.DataTransmission.DataSent);
        Assert.Equal(updateCount * 30, cumulative.DataTransmission.DataReceived);
        Assert.Equal(updateCount, Assert.Single(cumulative.ResponseCodes.ResponseSummaries).Count);
    }
}