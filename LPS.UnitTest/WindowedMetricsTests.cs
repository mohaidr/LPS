using System.Net;
using System.Net.Http;
using System.Text.Json;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Monitoring.Windowed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace LPS.UnitTest;

public sealed class WindowedMetricsTests
{
    [Fact]
    public async Task Collector_PreservesIdleWindowsBetweenBurstsUntilFinalization()
    {
        var iteration = new HttpIteration(new HttpIteration.SetupCommand
        {
            Name = "Burst", Mode = IterationMode.R, RequestCount = 2
        }, Mock.Of<IExpressionEvaluator>(), Mock.Of<ILogger>(), Mock.Of<IRuntimeOperationIdProvider>());
        var status = EntityExecutionStatus.Scheduled;
        var monitor = new Mock<IIterationStatusMonitor>();
        monitor.Setup(value => value.GetTerminalStatusAsync(iteration, It.IsAny<CancellationToken>())).ReturnsAsync(() => status);
        Func<Task>? closeWindow = null;
        var coordinator = new Mock<IWindowedMetricsCoordinator>();
        coordinator.SetupAdd(value => value.OnWindowClosed += It.IsAny<Func<Task>>())
            .Callback<Func<Task>>(handler => closeWindow = handler);
        var snapshots = new List<WindowedIterationSnapshot>();
        var queue = new Mock<IWindowedMetricsQueue>();
        queue.Setup(value => value.TryEnqueue(It.IsAny<WindowedIterationSnapshot>()))
            .Callback<WindowedIterationSnapshot>(snapshots.Add).Returns(true);
        var store = new Mock<IHistoricalWindowedMetricDataStore>();
        var cooling = new Mock<ICoolingTracker>();
        var periods = new[] { new CoolingPeriod("Watchdog", DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow) };
        cooling.Setup(value => value.GetPeriods(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), iteration.Id)).Returns(periods);
        using var throughput = new WindowedThroughputAggregator(iteration, "Main");
        using var transfer = new WindowedDataTransmissionAggregator(iteration, "Main");
        using var collector = new WindowedIterationMetricsCollector(iteration, "Main", queue.Object, store.Object,
            coordinator.Object, monitor.Object, Mock.Of<IPlanExecutionContext>(), cooling.Object)
        {
            ThroughputAggregator = throughput,
            DataTransmissionAggregator = transfer
        };

        await closeWindow!();
        Assert.Empty(snapshots);
        status = EntityExecutionStatus.Ongoing;
        await throughput.IncreaseConnectionsCount(CancellationToken.None);
        await throughput.DecreseConnectionsCount(CancellationToken.None);
        await transfer.UpdateDataReceivedAsync(100, CancellationToken.None);
        await closeWindow();
        await closeWindow();
        await throughput.IncreaseConnectionsCount(CancellationToken.None);
        await throughput.DecreseConnectionsCount(CancellationToken.None);
        await closeWindow();
        status = EntityExecutionStatus.Success;
        await closeWindow();
        await closeWindow();

        Assert.Equal(new[] { 1, 0, 1, 0 }, snapshots.Select(snapshot => snapshot.Throughput!.RequestsCount));
        Assert.Equal(new[] { false, true, false, true }, snapshots.Select(snapshot => snapshot.IsIdle));
        Assert.Equal(new[] { 1, 2, 3, 4 }, snapshots.Select(snapshot => snapshot.WindowSequence));
        Assert.True(snapshots[^1].IsFinal);
        Assert.All(snapshots, snapshot => Assert.Same(periods, snapshot.CoolingPeriods));
        Assert.Equal(snapshots[0].WindowEnd, snapshots[1].WindowStart);
        Assert.Equal(0, snapshots[1].DataTransmission!.DataReceived);
        store.Verify(value => value.PushAsync(iteration.Id, It.IsAny<WindowedIterationSnapshot>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public void EmptyWindow_IsIdleAndSerializesForTheDashboard()
    {
        var snapshot = CreateSnapshot();

        Assert.True(snapshot.HasData);
        Assert.True(snapshot.IsIdle);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot));
        Assert.True(json.RootElement.GetProperty("IsIdle").GetBoolean());
        Assert.False(new WindowedIterationSnapshot().IsIdle);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0)]
    [InlineData(0, 1, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 0)]
    [InlineData(0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 1)]
    public void WorkInProgressOrCompleted_IsNotIdle(int requests, int concurrent, int successful, int failed, int skipped)
    {
        var snapshot = CreateSnapshot(throughput: new WindowedThroughputData
        {
            RequestsCount = requests,
            MaxConcurrentRequests = concurrent,
            SuccessfulRequestCount = successful,
            FailedRequestsCount = failed,
            SkippedRequestsCount = skipped
        });

        Assert.False(snapshot.IsIdle);
        Assert.Contains("is_idle=false", InfluxDBLineProtocolConverter.ConvertWindowedSnapshot(snapshot));
    }

    [Fact]
    public void LateTimingOrTransferSamples_AreNotIdle()
    {
        Assert.False(CreateSnapshot(duration: new WindowedDurationData
        {
            TotalTime = new WindowedTimingMetric { Count = 1, Average = 0 }
        }).IsIdle);
        Assert.False(CreateSnapshot(transfer: new WindowedDataTransmissionData { DataReceived = 1 }).IsIdle);
        Assert.False(CreateSnapshot(transfer: new WindowedDataTransmissionData { DataSent = 1 }).IsIdle);
        Assert.False(new WindowedIterationSnapshot
        {
            Throughput = new(),
            ResponseCodes = new() { ResponseSummaries = new() { new() { HttpStatusCode = HttpStatusCode.OK, Count = 1 } } }
        }.IsIdle);
    }

    [Fact]
    public void IdleWindow_ExportsZeroActivityWithoutInventingLatency()
    {
        var snapshot = CreateSnapshot();
        var lines = InfluxDBLineProtocolConverter.ConvertWindowedSnapshot(snapshot).Split('\n');
        var requests = Assert.Single(lines, line => line.StartsWith("windowed_requests,"));
        var transfer = Assert.Single(lines, line => line.StartsWith("windowed_data_transfer,"));
        var timestamp = new DateTimeOffset(snapshot.WindowEnd).ToUnixTimeMilliseconds() * 1_000_000;

        Assert.Equal(2, lines.Length);
        Assert.Contains("requests_count=0i", requests);
        Assert.Contains("requests_per_second=0.00", requests);
        Assert.Contains("window_duration_ms=5000.00", requests);
        Assert.Contains("is_idle=true", requests);
        Assert.Contains("data_received=0.00", transfer);
        Assert.EndsWith($" {timestamp}", requests);
        Assert.EndsWith($" {timestamp}", transfer);
    }

    [Fact]
    public void ActiveWindow_ExportsRateAndMeasuredZeroLatency()
    {
        var snapshot = CreateSnapshot(
            throughput: new WindowedThroughputData { RequestsCount = 2, RequestsPerSecond = 0.4 },
            duration: new WindowedDurationData { TotalTime = new WindowedTimingMetric { Count = 2, Average = 0 } });
        var lines = InfluxDBLineProtocolConverter.ConvertWindowedSnapshot(snapshot).Split('\n');

        Assert.Contains(lines, line => line.StartsWith("windowed_duration,") && line.Contains("metric=total_time") && line.Contains("avg=0.00"));
        Assert.Contains(lines, line => line.StartsWith("windowed_requests,") && line.Contains("requests_per_second=0.40") && line.Contains("is_idle=false"));
    }

    [Fact]
    public async Task Writer_PostsIdleWindowWithNanosecondPrecision()
    {
        string? payload = null;
        Uri? uri = null;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, token) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("text/plain", request.Content!.Headers.ContentType!.MediaType);
                uri = request.RequestUri;
                payload = await request.Content.ReadAsStringAsync(token);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });
        using var client = new HttpClient(handler.Object);
        using var writer = new InfluxDBWriter(new InfluxDBOptions
        {
            Enabled = true, Url = "http://localhost:8086", Token = "test-token", Organization = "test org", Bucket = "test bucket"
        }, NullLogger<InfluxDBWriter>.Instance, client);

        var snapshot = CreateSnapshot();
        await writer.UploadWindowedMetricsAsync(snapshot);

        Assert.Equal("/api/v2/write?org=test%20org&bucket=test%20bucket&precision=ns", uri!.PathAndQuery);
        Assert.Equal(InfluxDBLineProtocolConverter.ConvertWindowedSnapshot(snapshot), payload);
        handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void CoolingDuringActiveRequests_ExportsBothSourcesAndActualBoundaries()
    {
        var start = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new WindowedIterationSnapshot
        {
            TestStartTime = start, WindowStart = start, WindowEnd = start.AddSeconds(5),
            Throughput = new WindowedThroughputData { RequestsCount = 20, MaxConcurrentRequests = 5 },
            CoolingPeriods = new[]
            {
                new CoolingPeriod("Watchdog", start.AddMilliseconds(250), start.AddSeconds(1)),
                new CoolingPeriod("BatchCooldown", start.AddMilliseconds(500), start.AddSeconds(2))
            }
        };

        var lines = InfluxDBLineProtocolConverter.ConvertWindowedSnapshot(snapshot).Split('\n');
        Assert.False(snapshot.IsIdle);
        var watchdog = Assert.Single(lines, line => line.StartsWith("windowed_cooling,") && line.Contains("source=Watchdog"));
        var batch = Assert.Single(lines, line => line.StartsWith("windowed_cooling,") && line.Contains("source=BatchCooldown"));
        Assert.Contains("duration_ms=750.00", watchdog);
        Assert.Contains("duration_ms=1500.00", batch);
        var end = new DateTimeOffset(start.AddSeconds(1)).ToUnixTimeMilliseconds() * 1_000_000;
        Assert.Contains($"end_ns={end}i", watchdog);
        Assert.EndsWith($" {end}", watchdog);
    }

    private static WindowedIterationSnapshot CreateSnapshot(
        WindowedThroughputData? throughput = null,
        WindowedDurationData? duration = null,
        WindowedDataTransmissionData? transfer = null)
    {
        var start = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);
        return new WindowedIterationSnapshot
        {
            PlanName = "Cooldown", RoundName = "Main", IterationName = "Burst", TargetUrl = "http://localhost",
            TestStartTime = start, WindowStart = start, WindowEnd = start.AddSeconds(5), WindowSequence = 1,
            Throughput = throughput ?? new(), Duration = duration ?? new(), DataTransmission = transfer ?? new(), ResponseCodes = new()
        };
    }
}