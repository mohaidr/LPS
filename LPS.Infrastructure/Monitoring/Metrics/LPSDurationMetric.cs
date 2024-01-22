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

namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSDurationMetric : ILPSResponseMetric
    {
        LPSHttpRun _httpRun;
        LongHistogram _histogram;
        ConsoleWriter.ConsoleWriter _consoleWriter;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }
        internal LPSDurationMetric(LPSHttpRun httpRun)
        {
            _httpRun = httpRun;
            _dimensionSet = new LPSDurationMetricDimensionSet();
            _histogram = new LongHistogram(1, 1000000, 3);
            _consoleWriter = new ConsoleWriter.ConsoleWriter(1, 1);
        }

        private LPSDurationMetricDimensionSet _dimensionSet { get; set; }
        public LPSDurationMetricDimensionSet DimensionSet { get { return _dimensionSet; } }
        public ResponseMetricType MetricType => ResponseMetricType.ResponseTime;

        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                double averageDenominator = _dimensionSet.AverageResponseTime != 0 ? _dimensionSet.SumResponseTime / _dimensionSet.AverageResponseTime + 1 : 1;
                _dimensionSet.MaxResponseTime = Math.Max(response.ResponseTime.TotalMilliseconds, _dimensionSet.MaxResponseTime);
                _dimensionSet.MinResponseTime = Math.Min(response.ResponseTime.TotalMilliseconds, _dimensionSet.MinResponseTime);
                _dimensionSet.SumResponseTime = _dimensionSet.SumResponseTime + response.ResponseTime.TotalMilliseconds;
                _dimensionSet.AverageResponseTime = _dimensionSet.SumResponseTime / averageDenominator;
                _histogram.RecordValue((long)response.ResponseTime.TotalMilliseconds);
                _dimensionSet.P10ResponseTime = _histogram.GetValueAtPercentile(10);
                _dimensionSet.P50ResponseTime = _histogram.GetValueAtPercentile(50);
                _dimensionSet.P90ResponseTime = _histogram.GetValueAtPercentile(90);
                _consoleWriter.AddMessage(this.Stringify(), 5, 2, ConsoleColor.Cyan);
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
                var toSerialize = DimensionSet.CloneObject();
                toSerialize.SumResponseTime = Math.Round(toSerialize.SumResponseTime, 2);
                toSerialize.MaxResponseTime = Math.Round(toSerialize.MaxResponseTime, 2);
                toSerialize.AverageResponseTime = Math.Round(toSerialize.AverageResponseTime, 2);
                toSerialize.MinResponseTime = Math.Round(toSerialize.MinResponseTime, 2);
                toSerialize.P10ResponseTime = Math.Round(toSerialize.P10ResponseTime, 2);
                toSerialize.P50ResponseTime = Math.Round(toSerialize.P50ResponseTime, 2);
                toSerialize.P90ResponseTime = Math.Round(toSerialize.P90ResponseTime, 2);
                return LPSSerializationHelper.Serialize(toSerialize);
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
        public double SumResponseTime { get; set; }
        public double AverageResponseTime { get; set; }
        public double MinResponseTime { get; set; }
        public double MaxResponseTime { get; set; }
        public double P90ResponseTime { get; set; }
        public double P50ResponseTime { get; set; }
        public double P10ResponseTime { get; set; }
    }

}
