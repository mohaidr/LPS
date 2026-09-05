using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.UnitTest
{
    public class DurationMetricTests
    {
        [Fact]
        public void LatencyMetric_NegativeValue_IsRecordedAsZero()
        {
            var metric = new DurationMetricSnapshot.LatencyMetric();

            metric.Update(-0.5);

            Assert.Equal(0, metric.Min);
            Assert.Equal(0, metric.Average);
            Assert.Equal(0, metric.P99);
        }

        [Fact]
        public void LatencyMetric_ZeroValues_AreIncludedInPercentiles()
        {
            var metric = new DurationMetricSnapshot.LatencyMetric();

            metric.Update(0);
            metric.Update(0);
            metric.Update(100);

            Assert.Equal(0, metric.P50);
            Assert.Equal(100, metric.P90);
        }

        [Fact]
        public void LatencyMetric_ValueAboveHistogramRange_DoesNotThrow()
        {
            var metric = new DurationMetricSnapshot.LatencyMetric();

            metric.Update(1000001);

            Assert.Equal(1000001, metric.Max);
            Assert.InRange(metric.P99, 1000000, 1001000);
        }

        [Fact]
        public void LatencyMetric_NonFiniteValue_IsIgnored()
        {
            var metric = new DurationMetricSnapshot.LatencyMetric();

            metric.Update(double.NaN);
            metric.Update(double.PositiveInfinity);

            Assert.Equal(0, metric.Sum);
            Assert.Equal(0, metric.Average);
        }
    }
}