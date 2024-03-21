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
namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSDurationMetric : ILPSResponseMetric
    {
        LPSHttpRun _httpRun;
        LongHistogram _histogram;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }
        internal LPSDurationMetric(LPSHttpRun httpRun)
        {
            _httpRun = httpRun;
            _dimensionSet = new LPSDurationMetricDimensionSet();
            _histogram = new LongHistogram(1, 1000000, 3);
            _groupId = Guid.NewGuid();
        }
        Guid _groupId;
        private LPSDurationMetricDimensionSet _dimensionSet { get; set; }
        public ResponseMetricType MetricType => ResponseMetricType.ResponseTime;

        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                double averageDenominator = _dimensionSet.AverageResponseTime != 0 ? (_dimensionSet.SumResponseTime / _dimensionSet.AverageResponseTime) + 1 : 1;
                _dimensionSet.MaxResponseTime = Math.Max(response.ResponseTime.TotalMilliseconds, _dimensionSet.MaxResponseTime);
                _dimensionSet.MinResponseTime = _dimensionSet.MinResponseTime == 0 ? response.ResponseTime.TotalMilliseconds: Math.Min(response.ResponseTime.TotalMilliseconds, _dimensionSet.MinResponseTime);
                _dimensionSet.SumResponseTime = _dimensionSet.SumResponseTime + response.ResponseTime.TotalMilliseconds;
                _dimensionSet.AverageResponseTime = _dimensionSet.SumResponseTime / averageDenominator;
                _histogram.RecordValue((long)response.ResponseTime.TotalMilliseconds);
                _dimensionSet.P10ResponseTime = _histogram.GetValueAtPercentile(10);
                _dimensionSet.P50ResponseTime = _histogram.GetValueAtPercentile(50);
                _dimensionSet.P90ResponseTime = _histogram.GetValueAtPercentile(90);
                _dimensionSet.EndPointDetails =  $"{_httpRun.Name} - {_httpRun.LPSHttpRequestProfile.HttpMethod} {_httpRun.LPSHttpRequestProfile.URL} HTTP/{_httpRun.LPSHttpRequestProfile.Httpversion}";
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
                var dimensionSet = _dimensionSet.CloneObject();
                dimensionSet.SumResponseTime = Math.Round(dimensionSet.SumResponseTime, 2);
                dimensionSet.MaxResponseTime = Math.Round(dimensionSet.MaxResponseTime, 2);
                dimensionSet.AverageResponseTime = Math.Round(dimensionSet.AverageResponseTime, 2);
                dimensionSet.MinResponseTime = Math.Round(dimensionSet.MinResponseTime, 2);
                dimensionSet.P10ResponseTime = Math.Round(dimensionSet.P10ResponseTime, 2);
                dimensionSet.P50ResponseTime = Math.Round(dimensionSet.P50ResponseTime, 2);
                dimensionSet.P90ResponseTime = Math.Round(dimensionSet.P90ResponseTime, 2);
                return LPSSerializationHelper.Serialize(dimensionSet);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                return string.Empty;
            }
        }
    }

    public class LPSDurationMetricDimensionSet
    {
        public string EndPointDetails { get; set; }
        public double SumResponseTime { get; set; }
        public double AverageResponseTime { get; set; }
        public double MinResponseTime { get; set; }
        public double MaxResponseTime { get; set; }
        public double P90ResponseTime { get; set; }
        public double P50ResponseTime { get; set; }
        public double P10ResponseTime { get; set; }
    }

}
