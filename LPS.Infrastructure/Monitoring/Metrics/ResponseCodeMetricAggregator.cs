using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Monitoring.EventSources;
using LPS.Infrastructure.Monitoring.MetricsVariables;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using LPS.Infrastructure.Monitoring.MetricsServices;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class ResponseCodeMetricAggregator : BaseMetricAggregator, IResponseMetricCollector
    {

        private const string MetricName = "ResponseCode";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ResponseMetricEventSource _eventSource;
        private ResponseCodeMetricSnapshot _snapshot { get; set; }

        // NEW: metrics variable service
        private readonly IMetricsVariableService _metricsVariableService;
        private readonly string _roundName;
        internal ResponseCodeMetricAggregator(
            HttpIteration httpIteration,
            string roundName,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService // NEW
        , IMetricDataStore metricDataStore) : base(httpIteration, logger, runtimeOperationIdProvider, metricDataStore)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _eventSource = ResponseMetricEventSource.GetInstance(_httpIteration);
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _snapshot = new ResponseCodeMetricSnapshot(
                roundName,
                _httpIteration.Id,
                _httpIteration.Name,
                _httpIteration.HttpRequest.HttpMethod,
                _httpIteration.HttpRequest.Url.Url,
                _httpIteration.HttpRequest.HttpVersion);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
            PushMetricAsync(default).Wait();
        }

        protected override IMetricShapshot Snapshot => _snapshot;

        public override LPSMetricType MetricType => LPSMetricType.ResponseCode;

        public IResponseMetricCollector Update(HttpResponse.SetupCommand response, CancellationToken token)
            => UpdateAsync(response, token).Result;

        public async Task<IResponseMetricCollector> UpdateAsync(HttpResponse.SetupCommand response, CancellationToken token)
        {
            bool isLockTaken;
            await _semaphore.WaitAsync(token);
            isLockTaken = true;
            try
            {
                _snapshot.Update(response);
                _eventSource.WriteResponseBreakDownMetrics(response.StatusCode);

                await PushMetricAsync(token); // NEW
            }
            finally
            {
                if (isLockTaken) _semaphore.Release();
            }
            return this;
        }

        // NEW: serialize and publish the dimension set to the variable system
        private async Task PushMetricAsync(CancellationToken token)
        {
            var json = JsonSerializer.Serialize(_snapshot, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            });

            await _metricsVariableService.PutMetricAsync(_roundName, _httpIteration.Name, MetricName, json, token);

            await _metricDataStore.PushAsync(_httpIteration, _snapshot, token);
        }


    }

    public class HttpResponseSummary(HttpStatusCode httpStatusCode, string httpStatusReason, int count)
    {
        public HttpStatusCode HttpStatusCode { get; private set; } = httpStatusCode;
        public string HttpStatusReason { get; private set; } = httpStatusReason;
        public int Count { get; set; } = count;
    }

    public class ResponseCodeMetricSnapshot : HttpMetricSnapshot
    {
        public ResponseCodeMetricSnapshot(string roundName, Guid iterationId, string iterationName, string httpMethod, string url, string httpVersion)
        {
            IterationId = iterationId;
            RoundName = roundName;
            IterationName = iterationName;
            HttpMethod = httpMethod;
            URL = url;
            HttpVersion = httpVersion;
            _responseSummaries = new ConcurrentBag<HttpResponseSummary>();
        }

        public void Update(HttpResponse.SetupCommand response)
        {
            var summary = _responseSummaries.FirstOrDefault(rs =>
                rs.HttpStatusCode == response.StatusCode &&
                rs.HttpStatusReason == response.StatusMessage);

            if (summary != null)
            {
                summary.Count += 1;
            }
            else
            {
                var instance = new HttpResponseSummary(
                    response.StatusCode,
                    response.StatusMessage,
                    1
                );
                _responseSummaries.Add(instance);
            }

            TimeStamp = DateTime.UtcNow;
        }

        public override LPSMetricType MetricType => LPSMetricType.ResponseCode;

        protected ConcurrentBag<HttpResponseSummary> _responseSummaries { get; private set; }

        public IList<HttpResponseSummary> ResponseSummaries => _responseSummaries.ToList();
    }
}