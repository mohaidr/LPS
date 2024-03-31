using HdrHistogram;
using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using System.Diagnostics.Tracing;
using LPS.Infrastructure.Monitoring.EventSources;
namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSDurationMetric : ILPSResponseMetric
    {
        private LPSDurationMetricDimensionSetProtected _dimensionSet { get; set; }
        LPSHttpRun _httpRun;
        LongHistogram _histogram;
        LPSResponseMetricEventSource _eventSource;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }
        internal LPSDurationMetric(LPSHttpRun httpRun)
        {
            _httpRun = httpRun;
            _eventSource = LPSResponseMetricEventSource.GetInstance(_httpRun);
            _dimensionSet = new LPSDurationMetricDimensionSetProtected();
            _histogram = new LongHistogram(1, 1000000, 3);
        }
        public LPSMetricType MetricType => LPSMetricType.ResponseTime;

        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                _dimensionSet.Update(response.ResponseTime.TotalMilliseconds, _httpRun, _histogram);
                _eventSource.WriteResponseTimeMetrics(response.ResponseTime.TotalMilliseconds);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public ILPSResponseMetric Update(LPSHttpResponse httpResponse)
        {
            return UpdateAsync(httpResponse).Result;
        }

        public string Stringify()
        {
            try
            {
                return LPSSerializationHelper.Serialize(_dimensionSet);
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public IDimensionSet GetDimensionSet()
        {
            return _dimensionSet;
        }

        private class LPSDurationMetricDimensionSetProtected : LPSDurationMetricDimensionSet
        {
            public void Update(double responseTime, LPSHttpRun httpRun, LongHistogram histogram)
            {
                double averageDenominator = AverageResponseTime != 0 ? (SumResponseTime / AverageResponseTime) + 1 : 1;
                TimeStamp = DateTime.Now;
                MaxResponseTime = Math.Max(responseTime, MaxResponseTime);
                MinResponseTime = MinResponseTime == 0 ? responseTime : Math.Min(responseTime, MinResponseTime);
                SumResponseTime = SumResponseTime + responseTime;
                AverageResponseTime = SumResponseTime / averageDenominator;
                histogram.RecordValue((long)responseTime);
                P10ResponseTime = histogram.GetValueAtPercentile(10);
                P50ResponseTime = histogram.GetValueAtPercentile(50);
                P90ResponseTime = histogram.GetValueAtPercentile(90);
                EndPointDetails = $"{httpRun.Name} - {httpRun.LPSHttpRequestProfile.HttpMethod} {httpRun.LPSHttpRequestProfile.URL} HTTP/{httpRun.LPSHttpRequestProfile.Httpversion}";
            }
        }
    }

    public class LPSDurationMetricDimensionSet: IDimensionSet
    {
        public DateTime TimeStamp { get; protected set; }
        public string EndPointDetails { get; protected set; }
        public double SumResponseTime { get; protected set; }
        public double AverageResponseTime { get; protected set; }
        public double MinResponseTime { get; protected set; }
        public double MaxResponseTime { get; protected set; }
        public double P90ResponseTime { get; protected set; }
        public double P50ResponseTime { get; protected set; }
        public double P10ResponseTime { get; protected set; }
    }
}
