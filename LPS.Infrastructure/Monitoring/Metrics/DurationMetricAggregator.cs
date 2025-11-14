using HdrHistogram;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.EventSources;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Monitoring.MetricsVariables;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static LPS.Infrastructure.Monitoring.Metrics.DurationMetricSnapshot;

namespace LPS.Infrastructure.Monitoring.Metrics
{
   public enum DurationMetricType
    {
        TotalTime,
        DownStreamTime,
        UpStreamTime,
        TLSHandshakeTime,
        TCPHandshakeTime
    }

    public class DurationMetricAggregator : BaseMetricAggregator, IDurationMetricCollector
    {
        private const string MetricName = "Duration";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly DurationMetricSnapshot _snapshot;
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
            _snapshot = new DurationMetricSnapshot(
                roundName,
                _httpIteration.Id,
                httpIteration.Name,
                httpIteration.HttpRequest.HttpMethod,
                httpIteration.HttpRequest.Url.Url,
                httpIteration.HttpRequest.HttpVersion, _logger);
            PushMetricAsync(default).Wait();

        }

        protected override IMetricShapshot Snapshot => _snapshot;

        public override LPSMetricType MetricType => LPSMetricType.Time;

        public async Task<IDurationMetricCollector> UpdateTotalTimeAsync(double totalTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.TotalTime, totalTime, _histogram, token);
                _eventSource.WriteTimeMetrics(totalTime);

                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateDownStreamTimeAsync(double downStreamTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.DownStreamTime, downStreamTime, _histogram, token);
                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateUpStreamTimeAsync(double upStreamTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.UpStreamTime, upStreamTime, _histogram, token);
                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateTLSHandshakeTimeAsync(double tlsHandshakeTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.TLSHandshakeTime, tlsHandshakeTime, _histogram, token);
                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateTCPHandshakeTimeAsync(double tcpHandshakeTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.TCPHandshakeTime, tcpHandshakeTime, _histogram, token);
                _eventSource.WriteTimeMetrics(tcpHandshakeTime);

                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }


        public override async ValueTask StopAsync(CancellationToken token)
        {
            if (IsStarted) IsStarted = false;
            await ValueTask.CompletedTask;
        }

        public override async ValueTask StartAsync(CancellationToken token)
        {
            if (!IsStarted) IsStarted = true;
            await ValueTask.CompletedTask;
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
    }

    public class DurationMetricSnapshot : HttpMetricSnapshot
    {
        ILogger _logger;
        public DurationMetricSnapshot(
            string roundName,
            Guid iterationId,
            string iterationName,
            string httpMethod,
            string url,
            string httpVersion,
            ILogger logger)
        {
            IterationId = iterationId;
            RoundName = roundName;
            IterationName = iterationName;
            HttpMethod = httpMethod;
            URL = url;
            HttpVersion = httpVersion;
            this._logger = logger;
        }

        public async ValueTask UpdateAsync(DurationMetricType metricType, double totalTime, LongHistogram histogram, CancellationToken token)
        {
            try
            {
                TimeStamp = DateTime.UtcNow;

                switch (metricType)
                {
                    case DurationMetricType.TotalTime:
                        TotalTimeMetrics.Update(totalTime, histogram);
                        break;
                    case DurationMetricType.DownStreamTime:
                        DownStreamTimeMetrics.Update(totalTime, histogram);
                        break;
                    case DurationMetricType.UpStreamTime:
                        UpStreamTimeMetrics.Update(totalTime, histogram);
                        break;
                    case DurationMetricType.TLSHandshakeTime:
                        SSLHandshakeTimeMetrics.Update(totalTime, histogram);
                        break;
                    case DurationMetricType.TCPHandshakeTime:
                        TCPHandshakeTimeMetrics.Update(totalTime, histogram);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(metricType), $"Unsupported DurationMetricType: {metricType}");
                }
            }
            catch (Exception ex)
            {
                await _logger.LogAsync($"Error updating DurationMetricSnapshot: {ex.Message}", LPSLoggingLevel.Error, token);
                throw;
            }

        }
        public override LPSMetricType MetricType => LPSMetricType.Time;
        public TotalTime TotalTimeMetrics { get; private set; } = new();
        public DownStreamTime DownStreamTimeMetrics { get; private set; } = new();
        public UpStreamTime UpStreamTimeMetrics { get; private set; } = new();
        public SSLHandshakeTime SSLHandshakeTimeMetrics { get; private set; } = new();
        public TCPHandshakeTime TCPHandshakeTimeMetrics { get; private set; } = new();
        
        public class MetricTime
        {
            public double Sum { get; private set; }
            public double Average { get; private set; }
            public double Min { get; private set; }
            public double Max { get; private set; }
            public double P90 { get; private set; }
            public double P50 { get; private set; }
            public double P10 { get; private set; }

            public void Update(double valueMs, LongHistogram histogram)
            {
                // incremental mean without keeping a counter field:
                // Average != 0 implies we've had N = Sum/Average samples so far
                double n = Average != 0 ? (Sum / Average) : 0;
                Max = Math.Max(valueMs, Max);
                Min = n == 0 ? valueMs : Math.Min(valueMs, Min);
                Sum += valueMs;
                Average = Sum / (n + 1);

                histogram.RecordValue((long)valueMs);
                P10 = histogram.GetValueAtPercentile(10);
                P50 = histogram.GetValueAtPercentile(50);
                P90 = histogram.GetValueAtPercentile(90);
            }
        }
        public class TotalTime: MetricTime {}
        public class DownStreamTime: MetricTime {}

        public class UpStreamTime : MetricTime {}

        public class SSLHandshakeTime: MetricTime {}

        public class TCPHandshakeTime: MetricTime {}

    }
}