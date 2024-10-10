using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Monitoring.EventSources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class ResponseCodeMetricCollector : BaseMetricCollector, IResponseMetricCollector
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        ResponseMetricEventSource _eventSource;

        internal ResponseCodeMetricCollector(HttpRun httpRun, ILogger logger , IRuntimeOperationIdProvider runtimeOperationIdProvider) : base(httpRun, logger, runtimeOperationIdProvider)
        {
            _httpRun = httpRun;
            _eventSource = ResponseMetricEventSource.GetInstance(_httpRun);
            _dimensionSet = new ProtectedResponseCodeDimensionSet(_httpRun.Name, _httpRun.LPSHttpRequestProfile.HttpMethod, _httpRun.LPSHttpRequestProfile.URL, _httpRun.LPSHttpRequestProfile.Httpversion);
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        protected override IDimensionSet DimensionSet => _dimensionSet;

        public override LPSMetricType MetricType => LPSMetricType.ResponseCode;
        private ProtectedResponseCodeDimensionSet _dimensionSet { get; set; }

        public IResponseMetricCollector Update(HttpResponse response)
        {
            return UpdateAsync(response).Result;
        }

        public async Task<IResponseMetricCollector> UpdateAsync(HttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                _dimensionSet.Update(response);
                _eventSource.WriteResponseBreakDownMetrics(response.StatusCode);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }


        public override void Stop()
        {
            IsStopped = true;
        }

        public override void Start()
        {
            IsStopped = false;
        }

        private class ProtectedResponseCodeDimensionSet : ResponseCodeDimensionSet
        {
            public ProtectedResponseCodeDimensionSet(string name, string httpMethod, string url, string httpVersion)
            {
                RunName = name;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }

            public void Update(HttpResponse response)
            {
                var existingSummary = _responseSummaries.FirstOrDefault(rs => rs.HttpStatusCode == ((int)response.StatusCode).ToString() && rs.HttpStatusReason == response.StatusMessage);
                if (existingSummary != null)
                {
                    existingSummary.Count += 1;
                }
                else
                {
                    var summary = new ResponseSummary(
                        ((int)response.StatusCode).ToString(),
                        response.StatusMessage,
                        1
                    );
                    _responseSummaries.Add(summary);
                }

                TimeStamp = DateTime.UtcNow;
            }
        }
    }
    public class ResponseSummary
    {
        public string HttpStatusCode { get; private set; }
        public string HttpStatusReason { get; private set; }
        public int Count { get; set; }

        public ResponseSummary(string httpStatusCode, string httpStatusReason, int count)
        {
            HttpStatusCode = httpStatusCode;
            HttpStatusReason = httpStatusReason;
            Count = count;
        }
    }
    public class ResponseCodeDimensionSet : IDimensionSet
    {

        public ResponseCodeDimensionSet()
        {
            _responseSummaries = new ConcurrentBag<ResponseSummary>();
        }

        public DateTime TimeStamp { get; protected set; }
        public string RunName { get; protected set; }
        public string URL { get; protected set; }
        public string HttpMethod { get; protected set; }
        public string HttpVersion { get; protected set; }

        protected ConcurrentBag<ResponseSummary> _responseSummaries { get; set; }

        public IList<ResponseSummary> ResponseSummary => _responseSummaries.ToList();
    }
}
