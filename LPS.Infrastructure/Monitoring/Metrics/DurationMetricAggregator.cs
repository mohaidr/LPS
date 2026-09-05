using HdrHistogram;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
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
        WaitingTime,        // TTFB - TCP - TLS
        ServerTime,         // Server processing time (total) from response header
        ServerTimeDB,       // Server-Timing: db;dur=X
        ServerTimeCache,    // Server-Timing: cache;dur=X
        ServerTimeApp       // Server-Timing: app;dur=X
    }

    public class DurationMetricAggregator : BaseMetricAggregator, IDurationMetricCollector, IDisposable
    {
        private const string MetricName = "Duration";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly DurationMetricSnapshot _snapshot;
        private readonly ResponseMetricEventSource _eventSource;
        private readonly IMetricsVariableService _metricsVariableService; // NEW
        private readonly string _roundName;
        private readonly int _publishIntervalMs;
        private Timer _timer;
        private bool _isStarted;
        private bool _disposed;
        internal DurationMetricAggregator(
            HttpIteration httpIteration,
            string roundName,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService,
            ILiveMetricDataStore metricDataStore,
            LiveMetricsPublishingOptions publishingOptions) // NEW
            : base(httpIteration, logger, runtimeOperationIdProvider, metricDataStore)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
            ArgumentNullException.ThrowIfNull(publishingOptions);
            _publishIntervalMs = publishingOptions.PublishIntervalMs;
            _eventSource = ResponseMetricEventSource.GetInstance(_httpIteration);
            _snapshot = new DurationMetricSnapshot(
                roundName,
                _httpIteration.Id,
                httpIteration.Name,
                httpIteration.HttpRequest.HttpMethod,
                httpIteration.HttpRequest.Url.Url,
                httpIteration.HttpRequest.HttpVersion, _logger);
            PushMetricAsync(_snapshot.CreateCopy(), default).Wait();

        }

        protected override IMetricShapshot Snapshot => _snapshot;

        public override LPSMetricType MetricType => LPSMetricType.Time;

        public async Task<IDurationMetricCollector> UpdateTotalTimeAsync(double totalTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.TotalTime, totalTime, token);
                _eventSource.WriteTimeMetrics(totalTime);
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
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.ReceivingTime, receivingTime, token);
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
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.SendingTime, sendingTime, token);
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
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.TLSHandshakeTime, tlsHandshakeTime, token);
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
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.TCPHandshakeTime, tcpHandshakeTime, token);
                _eventSource.WriteTimeMetrics(tcpHandshakeTime);
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
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.TimeToFirstByte, timeToFirstByte, token);
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
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.WaitingTime, waitingTime, token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateServerTimeAsync(double serverTime, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.ServerTime, serverTime, token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateServerTimeDBAsync(double serverTimeDB, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.ServerTimeDB, serverTimeDB, token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateServerTimeCacheAsync(double serverTimeCache, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.ServerTimeCache, serverTimeCache, token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        public async Task<IDurationMetricCollector> UpdateServerTimeAppAsync(double serverTimeApp, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                EnsureStarted();
                await _snapshot.UpdateAsync(DurationMetricType.ServerTimeApp, serverTimeApp, token);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }

        private void EnsureStarted()
        {
            if (_isStarted || _disposed) return;
            _isStarted = true;
            _timer = new Timer(OnMetricsUpdate, state: null, dueTime: _publishIntervalMs, period: Timeout.Infinite);
        }

        private async void OnMetricsUpdate(object state)
        {
            if (!_isStarted || _disposed) return;

            bool lockTaken = false;
            try
            {
                await _semaphore.WaitAsync().ConfigureAwait(false);
                lockTaken = true;
                var snapshot = _snapshot.CreateCopy();
                _semaphore.Release();
                lockTaken = false;

                await PushMetricAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) when (_disposed)
            {
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed to publish duration metrics\n{ex}", LPSLoggingLevel.Error);
            }
            finally
            {
                if (lockTaken)
                    _semaphore.Release();

                if (!_disposed)
                {
                    try
                    {
                        _timer?.Change(_publishIntervalMs, Timeout.Infinite);
                    }
                    catch (ObjectDisposedException) when (_disposed)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current cumulative duration data for the CumulativeIterationMetricsCollector.
        /// Similar to how WindowedDurationAggregator.GetWindowDataAndReset() works, but does NOT reset.
        /// </summary>
        /// </summary>
        #nullable enable
        public CumulativeDurationData? GetCumulativeData()
        {
            _semaphore.Wait();
            try
            {
                return new CumulativeDurationData
                {
                    TotalTime = MapTimingMetric(_snapshot.TotalTime),
                    TCPHandshakeTime = MapTimingMetric(_snapshot.TCPHandshakeTime),
                    SSLHandshakeTime = MapTimingMetric(_snapshot.SSLHandshakeTime),
                    TimeToFirstByte = MapTimingMetric(_snapshot.TimeToFirstByte),
                    WaitingTime = MapTimingMetric(_snapshot.WaitingTime),
                    ReceivingTime = MapTimingMetric(_snapshot.ReceivingTime),
                    SendingTime = MapTimingMetric(_snapshot.SendingTime),
                    ServerTime = MapTimingMetric(_snapshot.ServerTime),
                    ServerTimeDB = MapTimingMetric(_snapshot.DBServerTime),
                    ServerTimeCache = MapTimingMetric(_snapshot.ServerCacheTime),
                    ServerTimeApp = MapTimingMetric(_snapshot.ServerAppTime)
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }
        #nullable restore

        private static CumulativeTimingMetric MapTimingMetric(DurationMetricSnapshot.LatencyMetric metric)
        {
            return new CumulativeTimingMetric
            {
                Sum = metric.Sum,
                Average = metric.Average,
                Min = metric.Min,
                Max = metric.Max,
                P50 = metric.P50,
                P90 = metric.P90,
                P95 = metric.P95,
                P99 = metric.P99
            };
        }

        private async Task PushMetricAsync(DurationMetricSnapshot snapshot, CancellationToken token)
        {
            var json = JsonSerializer.Serialize(snapshot, MetricJsonSerializerOptions);

            await _metricsVariableService.PutMetricAsync(_roundName, _httpIteration.Name, MetricName, json, token);

            await _metricDataStore.PushAsync(_httpIteration, snapshot, token);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
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
                        TotalTime.Update(totalTime);
                        break;
                    case DurationMetricType.ReceivingTime:
                        ReceivingTime.Update(totalTime);
                        break;
                    case DurationMetricType.SendingTime:
                        SendingTime.Update(totalTime);
                        break;
                    case DurationMetricType.TLSHandshakeTime:
                        SSLHandshakeTime.Update(totalTime);
                        break;
                    case DurationMetricType.TCPHandshakeTime:
                        TCPHandshakeTime.Update(totalTime);
                        break;
                    case DurationMetricType.TimeToFirstByte:
                        TimeToFirstByte.Update(totalTime);
                        break;
                    case DurationMetricType.WaitingTime:
                        WaitingTime.Update(totalTime);
                        break;
                    case DurationMetricType.ServerTime:
                        ServerTime.Update(totalTime);
                        break;
                    case DurationMetricType.ServerTimeDB:
                        DBServerTime.Update(totalTime);
                        break;
                    case DurationMetricType.ServerTimeCache:
                        ServerCacheTime.Update(totalTime);
                        break;
                    case DurationMetricType.ServerTimeApp:
                        ServerAppTime.Update(totalTime);
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

        public DurationMetricSnapshot CreateCopy()
        {
            var copy = new DurationMetricSnapshot(RoundName, IterationId, IterationName, HttpMethod, URL, HttpVersion, _logger)
            {
                TimeStamp = TimeStamp
            };

            copy.TotalTime.CopyFrom(TotalTime);
            copy.TCPHandshakeTime.CopyFrom(TCPHandshakeTime);
            copy.SSLHandshakeTime.CopyFrom(SSLHandshakeTime);
            copy.TimeToFirstByte.CopyFrom(TimeToFirstByte);
            copy.WaitingTime.CopyFrom(WaitingTime);
            copy.ReceivingTime.CopyFrom(ReceivingTime);
            copy.SendingTime.CopyFrom(SendingTime);
            copy.ServerTime.CopyFrom(ServerTime);
            copy.DBServerTime.CopyFrom(DBServerTime);
            copy.ServerCacheTime.CopyFrom(ServerCacheTime);
            copy.ServerAppTime.CopyFrom(ServerAppTime);
            return copy;
        }

        public override LPSMetricType MetricType => LPSMetricType.Time;
        
        // Cumulative metrics (never reset - for final summary)
        public TotalLatency TotalTime { get; private set; } = new();
        public TCPHandshakeLatency TCPHandshakeTime { get; private set; } = new();
        public SSLHandshakeLatency SSLHandshakeTime { get; private set; } = new();
        public TimeToFirstByteLatency TimeToFirstByte { get;private set; } = new();
        public WaitingLatency WaitingTime { get; private set; } = new();
        public ReceivingLatency ReceivingTime { get; private set; } = new();
        public SendingLatency SendingTime { get; private set; } = new();
        public ServerLatency ServerTime { get; private set; } = new();
        public ServerDBLatency DBServerTime { get; private set; } = new();
        public ServerCacheLatency ServerCacheTime { get; private set; } = new();
        public ServerAppLatency ServerAppTime { get; private set; } = new();


        public class LatencyMetric
        {
            private readonly LongHistogram _histogram = new(1, 1000000, 3);  // Each instance has its own
            private long _count;

            public double Sum { get; private set; }
            public double Average { get; private set; }
            public double Min { get; private set; }
            public double Max { get; private set; }
            public double P50 { get; private set; }
            public double P90 { get; private set; }
            public double P95 { get; private set; }
            public double P99 { get; private set; }

            public void CopyFrom(LatencyMetric source)
            {
                _count = source._count;
                Sum = source.Sum;
                Average = source.Average;
                Min = source.Min;
                Max = source.Max;
                P50 = source.P50;
                P90 = source.P90;
                P95 = source.P95;
                P99 = source.P99;
            }

            public void Update(double valueMs)
            {
                if (!double.IsFinite(valueMs))
                    return;

                valueMs = Math.Max(0, valueMs);
                _count++;
                Max = Math.Max(valueMs, Max);
                Min = _count == 1 ? valueMs : Math.Min(valueMs, Min);
                Sum += valueMs;
                Average = Sum / _count;

                _histogram.RecordValue(Math.Clamp((long)Math.Ceiling(valueMs), 0, 1000000));
                if (_histogram.TotalCount > 0)
                {
                    P50 = _histogram.GetValueAtPercentile(50);
                    P90 = _histogram.GetValueAtPercentile(90);
                    P95 = _histogram.GetValueAtPercentile(95);
                    P99 = _histogram.GetValueAtPercentile(99);
                }
            }
        }

        public class TotalLatency : LatencyMetric { }
        public class SSLHandshakeLatency : LatencyMetric { }
        public class TCPHandshakeLatency : LatencyMetric { }
        public class TimeToFirstByteLatency : LatencyMetric { }
        public class WaitingLatency : LatencyMetric { }
        public class ReceivingLatency : LatencyMetric { }
        public class SendingLatency : LatencyMetric { }
        public class ServerLatency : LatencyMetric { }
        public class ServerDBLatency : LatencyMetric { }
        public class ServerCacheLatency : LatencyMetric { }
        public class ServerAppLatency : LatencyMetric { }
    }
}   