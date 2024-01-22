using System;
using System.Diagnostics.Tracing;
using System.Threading;
using LPS.Domain;

namespace LPS.Infrastructure.Monitoring
{
    [EventSource(Name = "lps.response.time")]
    internal class LPSResponseMetricEventSource : EventSource
    {
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private static readonly Lazy<LPSResponseMetricEventSource> lazyInstance = new Lazy<LPSResponseMetricEventSource>(() => new LPSResponseMetricEventSource());
        public static LPSResponseMetricEventSource Log => lazyInstance.Value;

        LPSHttpRun _lpshttpRun;
        private EventCounter _maxResponseCounter;
        private EventCounter _minResponseCounter;
        private EventCounter _averageResponseCounter;
        private EventCounter _p10ResponseCounter;
        private EventCounter _p50ResponseCounter;
        private EventCounter _p90ResponseCounter;

        private LPSResponseMetricEventSource()
        {
            _averageResponseCounter = new EventCounter("average-response-time", this)
            {
                DisplayName = $"{_lpshttpRun.Name} Average Response Time",
                DisplayUnits = "ms"
            };

            _maxResponseCounter = new EventCounter("max-response-time", this)
            {
                DisplayName = $"{_lpshttpRun.Name} Max Response Time",
                DisplayUnits = "ms"
            };

            _minResponseCounter = new EventCounter("min-response-time", this)
            {
                DisplayName = $"{_lpshttpRun.Name} Min Response Time",
                DisplayUnits = "ms"
            };

            _p10ResponseCounter = new EventCounter("p10-response-time", this)
            {
                DisplayName = $"{_lpshttpRun.Name} 10th Percentile Response Time",
                DisplayUnits = "ms"
            };

            _p50ResponseCounter = new EventCounter("p50-response-time", this)
            {
                DisplayName = $"{_lpshttpRun.Name} 50th Percentile Response Time",
                DisplayUnits = "ms"
            };

            _p90ResponseCounter = new EventCounter("p90-response-time", this)
            {
                DisplayName = $"{_lpshttpRun.Name} 90th Percentile Response Time",
                DisplayUnits = "ms"
            };

        }

        [Event(1, Message = "Response Time Metrics: {Http Run Title} {0}. Max Response Time {1}, Max Response Time {2}, Average Response Time {3}, Percentile 10 Response Time {4}, Percentile 50 Response Time {5}, Percentile 90 Response Time {6}")]
        public void WriteResponseDurationMetrics(string message, double maxReponseTime, double minResponseTime, double averageResponseTime, double percentile10ResponseTime, double percentile50ResponseTime, double percentile90ResponseTime)
        {
            try
            {
                WriteEvent(1, maxReponseTime, minResponseTime, averageResponseTime, percentile10ResponseTime, percentile50ResponseTime, percentile90ResponseTime);
                _averageResponseCounter?.WriteMetric(averageResponseTime);
                _maxResponseCounter?.WriteMetric(maxReponseTime);
                _minResponseCounter?.WriteMetric(minResponseTime);
                _p10ResponseCounter?.WriteMetric(percentile10ResponseTime);
                _p50ResponseCounter?.WriteMetric(percentile50ResponseTime);
                _p90ResponseCounter?.WriteMetric(percentile90ResponseTime);
            }
            catch (Exception ex) 
            {
                
            }
        }

        [Event(2, Message = "Response Code Breakdown: {0}")]
        public void WriteResponseDurationMetrics(string message)
        {
            try
            {
                WriteEvent(1, message);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
