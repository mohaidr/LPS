using System.Net;
using System.Linq;
using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Monitoring.Hosts;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.UnitTest;

public sealed class HostMetricsAggregatorTests
{
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

        var firstWindow = aggregator.GetWindowedSnapshotAndReset();
        var emptyWindow = aggregator.GetWindowedSnapshotAndReset();
        var cumulative = aggregator.GetCumulativeSnapshot();

        Assert.Equal(1, firstWindow.Throughput.RequestsCount);
        Assert.Equal(2, firstWindow.Duration.TotalTime.Count);
        Assert.Equal(150, firstWindow.Duration.TotalTime.Average);
        Assert.Equal(30, firstWindow.DataTransmission.DataSent);
        Assert.Equal(70, firstWindow.DataTransmission.DataReceived);
        Assert.Single(firstWindow.ResponseCodes.ResponseSummaries);

        Assert.Equal(0, emptyWindow.Throughput.RequestsCount);
        Assert.Equal(0, emptyWindow.Duration.TotalTime.Count);
        Assert.Empty(emptyWindow.ResponseCodes.ResponseSummaries);

        Assert.Equal(1, cumulative.Throughput.RequestsCount);
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
        var cumulative = aggregator.GetCumulativeSnapshot();

        Assert.Equal(updateCount, cumulative.Throughput.RequestsCount);
        Assert.Equal(updateCount, cumulative.Throughput.SuccessfulRequestCount);
        Assert.Equal(10, cumulative.Duration.TotalTime?.Average);
        Assert.Equal(updateCount * 20, cumulative.DataTransmission.DataSent);
        Assert.Equal(updateCount * 30, cumulative.DataTransmission.DataReceived);
        Assert.Equal(updateCount, Assert.Single(cumulative.ResponseCodes.ResponseSummaries).Count);
    }
}