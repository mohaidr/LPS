using LPS.Infrastructure.Monitoring.Metrics;
using LPS.UI.Common.Options;

namespace LPS.UI.Core.Web.Models;

/// <summary>The editable LPS settings used by future runs.</summary>
public sealed record WorkspaceSettings
{
    public FileLoggerOptions FileLogger { get; init; } = new();
    public WatchdogOptions Watchdog { get; init; } = new();
    public HttpClientOptions HttpClient { get; init; } = new();
    public DashboardConfigurationOptions Dashboard { get; init; } = new();
    public LiveMetricsPublishingOptions LiveMetrics { get; init; } = new();
    public InfluxDBOptions InfluxDB { get; init; } = new();
}