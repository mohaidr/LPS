using HdrHistogram;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.EventSources;
using LPS.Infrastructure.Monitoring.MetricsVariables;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.MetricsServices;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class DurationMetricAggregator : BaseMetricAggregator, IResponseMetricCollector
    {
        private const string MetricName = "Duration";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly LPSDurationMetricSnapshotProtected _snapshot;
        private readonly LongHistogram _histogram;
        private readonly ResponseMetricEventSource _eventSource;
        private readonly IMetricsVariableService _metricsVariableService; // NEW
        private readonly string _roundName;
        internal DurationMetricAggregator(
            HttpIteration httpIteration,
            string roundName,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService,
            IMetricDataStore metricDataStore) // NEW
            : base(httpIteration, logger, runtimeOperationIdProvider, metricDataStore)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _histogram = new LongHistogram(1, 1000000, 3);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
            _eventSource = ResponseMetricEventSource.GetInstance(_httpIteration);
            _snapshot = new LPSDurationMetricSnapshotProtected(
                roundName,
                _httpIteration.Id,
                httpIteration.Name,
                httpIteration.HttpRequest.HttpMethod,
                httpIteration.HttpRequest.Url.Url,
                httpIteration.HttpRequest.HttpVersion);
            PushMetricAsync(default).Wait();

        }

        protected override IMetricShapshot Snapshot => _snapshot;

        public override LPSMetricType MetricType => LPSMetricType.ResponseTime;

        public async Task<IResponseMetricCollector> UpdateAsync(HttpResponse.SetupCommand response, CancellationToken token)
        {
            await _semaphore.WaitAsync();
            try
            {
                _snapshot.Update(response.TotalTime.TotalMilliseconds, _histogram);
                _eventSource.WriteResponseTimeMetrics(response.TotalTime.TotalMilliseconds);

                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public IResponseMetricCollector Update(HttpResponse.SetupCommand httpResponse, CancellationToken token)
        {
            return UpdateAsync(httpResponse, token).Result;
        }

        public override void Stop()
        {
            if (IsStarted)
            {
                IsStarted = false;
            }
        }

        public override void Start()
        {
            if (!IsStarted)
            {
                IsStarted = true;
            }
        }

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

        private class LPSDurationMetricSnapshotProtected : DurationMetricSnapshot
        {
            public LPSDurationMetricSnapshotProtected(
                string roundName,
                Guid iterationId,
                string iterationName,
                string httpMethod,
                string url,
                string httpVersion)
            {
                IterationId = iterationId;
                RoundName = roundName;
                IterationName = iterationName;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }

            public void Update(double responseTime, LongHistogram histogram)
            {
                double averageDenominator = AverageResponseTime != 0
                    ? (SumResponseTime / AverageResponseTime) + 1
                    : 1;

                TimeStamp = DateTime.UtcNow;
                MaxResponseTime = Math.Max(responseTime, MaxResponseTime);
                MinResponseTime = MinResponseTime == 0 ? responseTime : Math.Min(responseTime, MinResponseTime);
                SumResponseTime += responseTime;
                AverageResponseTime = SumResponseTime / averageDenominator;

                histogram.RecordValue((long)responseTime);
                P10ResponseTime = histogram.GetValueAtPercentile(10);
                P50ResponseTime = histogram.GetValueAtPercentile(50);
                P90ResponseTime = histogram.GetValueAtPercentile(90);
            }
        }
    }

    public class DurationMetricSnapshot : HttpMetricSnapshot
    {
        public override LPSMetricType MetricType => LPSMetricType.ResponseTime;

        public double SumResponseTime { get; protected set; }
        public double AverageResponseTime { get; protected set; }
        public double MinResponseTime { get; protected set; }
        public double MaxResponseTime { get; protected set; }
        public double P90ResponseTime { get; protected set; }
        public double P50ResponseTime { get; protected set; }
        public double P10ResponseTime { get; protected set; }
    }
}