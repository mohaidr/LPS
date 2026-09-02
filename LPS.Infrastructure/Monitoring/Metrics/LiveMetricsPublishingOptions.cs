namespace LPS.Infrastructure.Monitoring.Metrics
{
    public sealed class LiveMetricsPublishingOptions
    {
        public int PublishIntervalMs { get; set; } = 250;
    }
}
