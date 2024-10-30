using LPS.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.EventSources
{
    [EventSource(Name = "lps.request.counter")]
    public class RequestEventSource : EventSource
    {
        private static readonly ConcurrentDictionary<HttpIteration, RequestEventSource> instances = new ConcurrentDictionary<HttpIteration, RequestEventSource>();

        private IncrementingEventCounter requestIncrementCounter;

        private RequestEventSource(HttpIteration lpshttpRun)
        {
            if (lpshttpRun != null && lpshttpRun.RequestProfile != null &&  Uri.TryCreate(lpshttpRun.RequestProfile.URL, UriKind.Absolute, out Uri uriResult))
            {
                this.requestIncrementCounter = new IncrementingEventCounter("requestsPerSecond", this)
                {
                    DisplayName = $"{lpshttpRun.RequestProfile.HttpMethod}.{uriResult.Scheme}.{uriResult.Host}.requests.per.second",
                    DisplayRateTimeScale = TimeSpan.FromSeconds(1) // This sets the rate to per second
                };
            }
            else
            { 
                throw new InvalidOperationException();
            }
        }

        public static RequestEventSource GetInstance(HttpIteration lpsHttpRun)
        {
            return instances.GetOrAdd(lpsHttpRun, (run) => new RequestEventSource(run));
        }

        public void AddRequest()
        {
            // This method should be called whenever a request is made
            this.requestIncrementCounter.Increment();
        }

        protected override void Dispose(bool disposing)
        {
            // Clean up the IncrementingEventCounter when the EventSource is disposed
            this.requestIncrementCounter?.Dispose();
            base.Dispose(disposing);
        }
    }
}
