#nullable enable
using System;
using HdrHistogram;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.Hosts
{
    internal sealed class HostTimingAccumulator
    {
        private readonly LongHistogram _histogram = new(1, 1000000, 3);
        private long _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max;

        public void Record(double value)
        {
            _count++;
            _sum += value;
            _min = Math.Min(_min, value);
            _max = Math.Max(_max, value);

            if (value > 0)
                _histogram.RecordValue(Math.Clamp((long)Math.Ceiling(value), 1, 1000000));
        }

        public CumulativeTimingMetric ToCumulativeMetric() => new()
        {
            Sum = _sum,
            Average = _count > 0 ? _sum / _count : 0,
            Min = _min == double.MaxValue ? 0 : _min,
            Max = _max,
            P50 = Percentile(50),
            P90 = Percentile(90),
            P95 = Percentile(95),
            P99 = Percentile(99)
        };

        public WindowedTimingMetric ToWindowedMetric() => new()
        {
            Count = (int)Math.Min(_count, int.MaxValue),
            Sum = _sum,
            Average = _count > 0 ? _sum / _count : 0,
            Min = _min == double.MaxValue ? 0 : _min,
            Max = _max,
            P50 = Percentile(50),
            P90 = Percentile(90),
            P95 = Percentile(95),
            P99 = Percentile(99)
        };

        private double Percentile(double percentile) =>
            _histogram.TotalCount > 0 ? _histogram.GetValueAtPercentile(percentile) : 0;
    }
}
