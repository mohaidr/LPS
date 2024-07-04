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
using LPS.Domain.Common.Interfaces;
using System.Diagnostics;
using System.Timers;
using System.Text.Json.Serialization;
using System.Net;
namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSDurationMetricMonitor : ILPSResponseMetric
    {
        private LPSDurationMetricDimensionSetProtected _dimensionSet { get; set; }
        LPSHttpRun _httpRun;
        LongHistogram _histogram;
        LPSResponseMetricEventSource _eventSource;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }
        ILPSLogger _logger;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public bool IsStopped { get; private set; }
        internal LPSDurationMetricMonitor(LPSHttpRun httpRun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider = default)
        {
            _httpRun = httpRun;
            _eventSource = LPSResponseMetricEventSource.GetInstance(_httpRun);
            _dimensionSet = new LPSDurationMetricDimensionSetProtected(httpRun.Name, httpRun.LPSHttpRequestProfile.HttpMethod, httpRun.LPSHttpRequestProfile.URL, httpRun.LPSHttpRequestProfile.Httpversion);
            _histogram = new LongHistogram(1, 1000000, 3);
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        public LPSMetricType MetricType => LPSMetricType.ResponseTime;

        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                _dimensionSet.Update(response.ResponseTime.TotalMilliseconds, _histogram);
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
        public TDimensionSet GetDimensionSet<TDimensionSet>() where TDimensionSet : IDimensionSet
        {
            // Check if _dimensionSet is of the requested type TDimensionSet
            if (_dimensionSet is TDimensionSet dimensionSet)
            {
                return dimensionSet;
            }
            else
            {
                _logger?.Log(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSDurationMetricMonitor", LPSLoggingLevel.Error);
                throw new InvalidCastException($"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSDurationMetricMonitor");
            }
        }

        public void Stop()
        {
            IsStopped = true;
        }
        public void Start()
        {
            IsStopped = false;
        }

        private class LPSDurationMetricDimensionSetProtected : LPSDurationMetricDimensionSet
        {
            public LPSDurationMetricDimensionSetProtected(string name, string httpMethod, string url, string httpVersion) {
                RunName = name;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }
            public void Update(double responseTime, LongHistogram histogram)
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
            }
        }
    }

    public class LPSDurationMetricDimensionSet: IDimensionSet
    {
        public DateTime TimeStamp { get; protected set; }
        public string RunName { get; protected set; }
        public string URL { get; protected set; }
        public string HttpMethod { get; protected set; }
        public string HttpVersion { get; protected set; }
        public double SumResponseTime { get; protected set; }
        public double AverageResponseTime { get; protected set; }
        public double MinResponseTime { get; protected set; }
        public double MaxResponseTime { get; protected set; }
        public double P90ResponseTime { get; protected set; }
        public double P50ResponseTime { get; protected set; }
        public double P10ResponseTime { get; protected set; }
    }
}
