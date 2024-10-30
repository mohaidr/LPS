using System;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Net;
using LPS.Domain;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.EventSources
{
    [EventSource(Name = "lps.response.time")]
    internal class ResponseMetricEventSource : EventSource
    {
        private static readonly ConcurrentDictionary<HttpIteration, ResponseMetricEventSource> instances = new ConcurrentDictionary<HttpIteration, ResponseMetricEventSource>();

        private HttpIteration _lpshttpRun;
        private EventCounter _responseTimeMetric;
        private IncrementingEventCounter _successCounter;
        private IncrementingEventCounter _clientErrorCounter;
        private IncrementingEventCounter _serverErrorCounter;
        private IncrementingEventCounter _redirectionCounter;

        // Private constructor to enforce use of GetInstance for instance creation
        private ResponseMetricEventSource(HttpIteration lpsHttpRun)
        {
            _lpshttpRun = lpsHttpRun;
            InitializeEventCounters();
        }

        // Public static method to get or create an instance of LPSResponseMetricEventSource
        public static ResponseMetricEventSource GetInstance(HttpIteration lpsHttpRun)
        {
            return instances.GetOrAdd(lpsHttpRun, (run) => new ResponseMetricEventSource(run));
        }

        private void InitializeEventCounters()
        {
            if (_lpshttpRun != null && _lpshttpRun.RequestProfile == null && Uri.TryCreate(_lpshttpRun.RequestProfile.URL, UriKind.Absolute, out Uri uriResult))
            {

                _responseTimeMetric = new EventCounter("response-time", this)
                {
                    DisplayName = $"{_lpshttpRun.RequestProfile.HttpMethod}.{uriResult.Scheme}.{uriResult.Host}.response.time",
                    DisplayUnits = "ms"
                };

                _successCounter = new IncrementingEventCounter("success-responses", this)
                {
                    DisplayName = $"{_lpshttpRun.RequestProfile.HttpMethod}.{uriResult.Scheme}.{uriResult.Host}.success.responses",

                };

                _redirectionCounter = new IncrementingEventCounter("redirection-responses", this)
                {
                    DisplayName = $"{_lpshttpRun.RequestProfile.HttpMethod}.{uriResult.Scheme}.{uriResult.Host}.redirection.responses",
                };

                _clientErrorCounter = new IncrementingEventCounter("client-error-responses", this)
                {
                    DisplayName = $"{_lpshttpRun.RequestProfile.HttpMethod}.{uriResult.Scheme}.{uriResult.Host}.client.error.responses",
                };

                _serverErrorCounter = new IncrementingEventCounter("server-error-responses", this)
                {
                    DisplayName = $"{_lpshttpRun.RequestProfile.HttpMethod}.{uriResult.Scheme}.{uriResult.Host}.server.error.responses"
                };
            }

        }
        public void WriteResponseTimeMetrics(double responseTime)
        {
            if (IsEnabled())
            {
                _responseTimeMetric.WriteMetric(responseTime);
            }
        }
        public void WriteResponseBreakDownMetrics(HttpStatusCode statusCode)
        {
            if (IsEnabled())
            {
                if ((int)statusCode >= 200 && (int)statusCode < 300)
                {
                    _successCounter.Increment(1);
                }
                else if ((int)statusCode >= 300 && (int)statusCode < 400)
                {
                    _redirectionCounter.Increment(1);
                }
                else if ((int)statusCode >= 400 && (int)statusCode < 500)
                {
                    _clientErrorCounter.Increment(1);
                }
                else if ((int)statusCode >= 500)
                {
                    _serverErrorCounter.Increment(1);
                }
            }
        }
    }
}
