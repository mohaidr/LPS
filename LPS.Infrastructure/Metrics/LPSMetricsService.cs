using HdrHistogram;
using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LPS.Infrastructure.Metrics.ILPSResponseMetric;
using static LPS.Infrastructure.Metrics.LPSResponseMetric;

namespace LPS.Infrastructure.Metrics
{
    public class LPSMetricsEnroller
    {
        public void Enroll(LPSHttpRun lpsHttpRun)
        {
            LPSResponseMetricsDataSource.Register(lpsHttpRun);
        }
    }

    internal static class LPSResponseMetricsDataSource
    {
        private static List<ILPSResponseMetric> _responseMetric = new List<ILPSResponseMetric>();
        internal static void Register(LPSHttpRun lpsHttpRun)
        {
            if (!_responseMetric.Any(metric => metric.LPSHttpRun == lpsHttpRun))
            {
                _responseMetric.Add(new LPSResponseMetric(lpsHttpRun));
                _responseMetric.Add(new LPSDurationMetric(lpsHttpRun));
            }
        }
        public static List<ILPSResponseMetric> Get(Func<ILPSResponseMetric, bool> predicate)
        {
            return _responseMetric.Where(metric => predicate(metric)).ToList();
        }
    }



    public interface ILPSResponseMetric
    {
        public enum ResponseMetricType
        {
            ResponseTime,
            ResponseBreakDown,
        }

        public ResponseMetricType MetricType { get; }

        public LPSHttpRun LPSHttpRun { get; }
        public ILPSResponseMetric Update(LPSHttpResponse httpResponse);
        public Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse httpResponse);
    }

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
        }

        private LPSDurationMetricDimensionSet _dimensionSet { get; set; }
        LPSDurationMetricDimensionSet DimensionSet { get { return _dimensionSet; } }
        public ResponseMetricType MetricType => ResponseMetricType.ResponseTime;

        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                double averageDenominator = ((response.ResponseTime.TotalMilliseconds / _dimensionSet.AverageResponseTime) + 1);
                _dimensionSet.MaxResponseTime = Math.Max(response.ResponseTime.TotalMilliseconds, _dimensionSet.MaxResponseTime);
                _dimensionSet.MinResponseTime = Math.Min(response.ResponseTime.TotalMilliseconds, _dimensionSet.MinResponseTime);
                _dimensionSet.SumResponseTime = _dimensionSet.SumResponseTime + response.ResponseTime.TotalMilliseconds;
                _dimensionSet.AverageResponseTime = _dimensionSet.SumResponseTime / averageDenominator;
                _histogram.RecordValue((long)response.ResponseTime.TotalMilliseconds);
                _dimensionSet.P10ResponseTime = _histogram.GetValueAtPercentile(10);
                _dimensionSet.P50ResponseTime = _histogram.GetValueAtPercentile(50);
                _dimensionSet.P90ResponseTime = _histogram.GetValueAtPercentile(90);

                Console.WriteLine($"P10: {_dimensionSet.P10ResponseTime}");
                Console.WriteLine($"P90: {_dimensionSet.P90ResponseTime}");
                Console.WriteLine($"P50: {_dimensionSet.P50ResponseTime} ");
                Console.WriteLine($"Sum: {_dimensionSet.SumResponseTime}");
                Console.WriteLine($"Max: {_dimensionSet.MaxResponseTime}");
                Console.WriteLine($"Min: {_dimensionSet.MinResponseTime}");
                Console.WriteLine($"Average: {_dimensionSet.AverageResponseTime}");
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

    }

    internal class LPSDurationMetricDimensionSet
    {
        internal double SumResponseTime { get; set; }
        internal double AverageResponseTime { get; set; }
        internal double MinResponseTime { get; set; }
        internal double MaxResponseTime { get; set; }
        internal double P90ResponseTime { get; set; }
        internal double P50ResponseTime { get; set; }
        internal double P10ResponseTime { get; set; }
    }

    public class LPSResponseMetric : ILPSResponseMetric
    {
        LPSHttpRun _httpRun;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }

        internal LPSResponseMetric(LPSHttpRun httpRun)
        {
            _httpRun = httpRun;
            _dimensionsList = new List<ResponseDimensionSet>();
        }
        public ResponseMetricType MetricType => ResponseMetricType.ResponseBreakDown;
        private List<ResponseDimensionSet> _dimensionsList { get; set; }
        List<ResponseDimensionSet> DimensionsList { get { return _dimensionsList; } }
        public ILPSResponseMetric Update(LPSHttpResponse response)
        {
            return UpdateAsync(response).Result;
        }
        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                var existingDimension = _dimensionsList.Find(d => d.StatusCode == response.StatusCode && d.StatusReason == response.StatusMessage);

                if (existingDimension != null)
                {
                    // If dimension exists, increment the count
                    existingDimension.Sum++;
                    Console.WriteLine($"existingDimension.Sum {existingDimension.Sum}");

                }
                else
                {
                    // If dimension doesn't exist, add a new one
                    _dimensionsList.Add(new ResponseDimensionSet
                    {
                        StatusCode = response.StatusCode,
                        StatusReason = response.StatusMessage,
                        Sum = 1
                    });
                }

            }
            finally { 
                _semaphore.Release(); 
            }
            return this;
        }
        internal class ResponseDimensionSet
        {
            public HttpStatusCode StatusCode { get; set; }
            public String StatusReason { get; set; }
            public int Sum { get; set; }
        }
    }


    [EventSource(Name = "lps.response.time")]
    public class LPSResponseMetricLogger : EventSource
    {
        public LPSResponseMetricLogger()
        {

        }
    }
}
