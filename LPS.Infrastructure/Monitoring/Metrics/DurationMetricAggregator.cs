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
        ReceivingTime,      // Renamed from DownStreamTime
        SendingTime,        // Renamed from UpStreamTime
        TLSHandshakeTime,
        TCPHandshakeTime,
        TimeToFirstByte,
        WaitingTime         // NEW: TTFB - TCP - TLS
    }

    public class DurationMetricAggregator : BaseMetricAggregator, IDurationMetricCollector
    {
        private const string MetricName = "Duration";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly DurationMetricSnapshot _snapshot;
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
                await _snapshot.UpdateAsync(DurationMetricType.TotalTime, totalTime, token);
                _eventSource.WriteTimeMetrics(totalTime);

                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateReceivingTimeAsync(double receivingTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.ReceivingTime, receivingTime, token);
                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateSendingTimeAsync(double sendingTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.SendingTime, sendingTime, token);
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
                await _snapshot.UpdateAsync(DurationMetricType.TLSHandshakeTime, tlsHandshakeTime, token);
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
                await _snapshot.UpdateAsync(DurationMetricType.TCPHandshakeTime, tcpHandshakeTime, token);
                _eventSource.WriteTimeMetrics(tcpHandshakeTime);

                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateTimeToFirstByteAsync(double timeToFirstByte, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.TimeToFirstByte, timeToFirstByte, token);
                await PushMetricAsync(token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateWaitingTimeAsync(double waitingTime, CancellationToken token) // NEW METHOD
        {
            await _semaphore.WaitAsync(token);
            try
            {
                await _snapshot.UpdateAsync(DurationMetricType.WaitingTime, waitingTime, token);
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

        public async ValueTask UpdateAsync(DurationMetricType metricType, double totalTime, CancellationToken token)
        {
            try
            {
                TimeStamp = DateTime.UtcNow;

                switch (metricType)
                {
                    case DurationMetricType.TotalTime:
                        TotalTimeMetrics.Update(totalTime);
                        break;
                    case DurationMetricType.ReceivingTime: // RENAMED
                        ReceivingTimeMetrics.Update(totalTime);
                        break;
                    case DurationMetricType.SendingTime:   // RENAMED
                        SendingTimeMetrics.Update(totalTime);
                        break;
                    case DurationMetricType.TLSHandshakeTime:
                        SSLHandshakeTimeMetrics.Update(totalTime);
                        break;
                    case DurationMetricType.TCPHandshakeTime:
                        TCPHandshakeTimeMetrics.Update(totalTime);
                        break;
                    case DurationMetricType.TimeToFirstByte:
                        TimeToFirstByteMetrics.Update(totalTime);
                        break;
                    case DurationMetricType.WaitingTime: // NEW: TTFB - TCP - TLS
                        WaitingTimeMetrics.Update(totalTime);
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
        public TCPHandshakeTime TCPHandshakeTimeMetrics { get; private set; } = new();
        public SSLHandshakeTime SSLHandshakeTimeMetrics { get; private set; } = new();
        public TimeToFirstByte TimeToFirstByteMetrics { get;private set; } = new();
        public WaitingTime WaitingTimeMetrics { get; private set; } = new();
        public ReceivingTime ReceivingTimeMetrics { get; private set; } = new(); // RENAMED
        public SendingTime SendingTimeMetrics { get; private set; } = new();     // RENAMED


        public class MetricTime
        {
            private readonly LongHistogram _histogram = new(1, 1000000, 3);  // Each instance has its own

            public double Sum { get; private set; }
            public double Average { get; private set; }
            public double Min { get; private set; }
            public double Max { get; private set; }
            public double P50 { get; private set; }
            public double P90 { get; private set; }
            public double P95 { get; private set; }
            public double P99 { get; private set; }

            public void Update(double valueMs)
            {
                // incremental mean without keeping a counter field:
                // Average != 0 implies we've had N = Sum/Average samples so far
                double n = Average != 0 ? (Sum / Average) : 0;
                Max = Math.Max(valueMs, Max);
                Min = n == 0 ? valueMs : Math.Min(valueMs, Min);
                Sum += valueMs;
                Average = Sum / (n + 1);

                _histogram.RecordValue((long)valueMs);
                P50 = _histogram.GetValueAtPercentile(50);
                P90 = _histogram.GetValueAtPercentile(90);
                P95 = _histogram.GetValueAtPercentile(95);
                P99 = _histogram.GetValueAtPercentile(99);
            }
        }
        public class TotalTime : MetricTime { }
        public class SSLHandshakeTime : MetricTime { }
        public class TCPHandshakeTime : MetricTime { }
        public class TimeToFirstByte : MetricTime { }
        public class WaitingTime : MetricTime { }
        public class ReceivingTime : MetricTime { } // RENAMED
        public class SendingTime : MetricTime { }   // RENAMED
                                                    
    }
}   