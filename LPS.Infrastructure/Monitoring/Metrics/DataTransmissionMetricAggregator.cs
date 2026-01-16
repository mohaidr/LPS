using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.MetricsServices;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class DataTransmissionMetricAggregator : BaseMetricAggregator, IDataTransmissionMetricAggregator, IDisposable
    {
        private const string MetricName = "DataTransmission";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly string _roundName;
        private readonly IMetricsVariableService _metricsVariableService;

        // Lifetime totals (do NOT reset across batches)
        private long _totalSentBytes = 0;   // upstream (request bodies)
        private long _totalRecvBytes = 0;   // downstream (response bodies)
        private int _requestsCount = 0;   // read from Throughput snapshot

        // Lifetime active wall-clock (resumes across Start/Stop)
        private readonly Stopwatch _watch = new();
        private Timer _timer;
        private bool _isStarted;
        private bool _disposed;

        private readonly DataTransmissionMetricSnapshot _snapshot;
        protected override IMetricShapshot Snapshot => _snapshot;

        public override LPSMetricType MetricType => LPSMetricType.DataTransmission;

        internal DataTransmissionMetricAggregator(
            HttpIteration httpIteration,
            string roundName,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService,
            ILiveMetricDataStore metricDataStore)
            : base(httpIteration, logger, runtimeOperationIdProvider, metricDataStore)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));

            _snapshot = new DataTransmissionMetricSnapshot(
                _roundName,
                httpIteration.Id,
                httpIteration.Name,
                httpIteration.HttpRequest.HttpMethod,
                httpIteration.HttpRequest.Url.Url,
                httpIteration.HttpRequest.HttpVersion);

            // Seed an initial snapshot
            _ = PushMetricAsync(CancellationToken.None);
        }

        /// <summary>
        /// Lazy start: starts the stopwatch and timer on first call.
        /// Called internally when first data is recorded.
        /// </summary>
        private void EnsureStarted()
        {
            if (_isStarted || _disposed) return;
            _isStarted = true;
            _snapshot.StopUpdate = false;
            _watch.Start();
            ScheduleMetricsUpdate();
        }

        /// <summary>
        /// Called on every uploaded chunk (e.g., every 65 KB).
        /// </summary>
        public async ValueTask UpdateDataSentAsync(double totalBytes, CancellationToken token = default)
        {
            bool lockTaken = false;
            try
            {
                await _semaphore.WaitAsync(token);
                lockTaken = true;

                // Lazy start on first data
                EnsureStarted();

                _totalSentBytes += (long)totalBytes;
                _requestsCount = GetRequestsCount();
            }
            finally
            {
                if (lockTaken) _semaphore.Release();
            }
        }

        /// <summary>
        /// Called on every downloaded chunk (e.g., every 65 KB).
        /// </summary>
        public async ValueTask UpdateDataReceivedAsync(double totalBytes, CancellationToken token = default)
        {
            bool lockTaken = false;
            try
            {
                await _semaphore.WaitAsync(token);
                lockTaken = true;

                // Lazy start on first data
                EnsureStarted();

                _totalRecvBytes += (long)totalBytes;
                _requestsCount = GetRequestsCount();
            }
            finally
            {
                if (lockTaken) _semaphore.Release();
            }
        }

        // ---- Timer (same safe pattern you used in Throughput) ----
        private void ScheduleMetricsUpdate()
        {
            _timer?.Dispose(); // avoid duplicate timers if Start called again

            _timer = new Timer(_ =>
            {
                if (!_isStarted || _disposed) return;

                try
                {
                    _semaphore.Wait();

                    // lifetime Bps = totals / elapsedActiveSeconds
                    UpdateAndPushAsync(CancellationToken.None)
                        .AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId,
                        $"Failed to update data transmission metrics\n{ex}", LPSLoggingLevel.Error);
                }
                finally
                {
                    if (_semaphore.CurrentCount == 0)
                        _semaphore.Release();
                }
            }, null, /*due*/ 0, /*period*/ 1000);
        }

        // Under lock: compute lifetime metrics + push
        private async ValueTask UpdateAndPushAsync(CancellationToken token)
        {
            double elapsedSec = _watch.Elapsed.TotalSeconds;
            if (elapsedSec < 0.001) elapsedSec = 0.001; // guard early ticks

            double upBps = _totalSentBytes / elapsedSec;
            double downBps = _totalRecvBytes / elapsedSec;
            double allBps = (_totalSentBytes + _totalRecvBytes) / elapsedSec;

            double wallMs = _watch.Elapsed.TotalMilliseconds;
            double avgSentPerReq = _requestsCount > 0 ? (double)_totalSentBytes / _requestsCount : 0d;
            double avgRecvPerReq = _requestsCount > 0 ? (double)_totalRecvBytes / _requestsCount : 0d;

            _snapshot.UpdateDataSent(_totalSentBytes, avgSentPerReq, upBps, wallMs);
            _snapshot.UpdateDataReceived(_totalRecvBytes, avgRecvPerReq, downBps, wallMs);
            _snapshot.UpdateAverageBytes(allBps, wallMs);

            await PushMetricAsync(token);
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

        // Read the canonical lifetime RequestsCount from Throughput snapshot
        private int GetRequestsCount()
        {
            if (_metricDataStore.TryGetLatest(_httpIteration.Id, LPSMetricType.Throughput, out ThroughputMetricSnapshot t))
                return t.RequestsCount;
            return 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _snapshot.StopUpdate = true;
            _watch.Stop();
            _timer?.Dispose();
            _timer = null;
            _semaphore.Dispose();
        }

        /// <summary>
        /// Gets the current cumulative data transmission data for the CumulativeIterationMetricsCollector.
        /// Similar to how WindowedDataTransmissionAggregator.GetWindowDataAndReset() works, but does NOT reset.
        /// </summary>
        #nullable enable
        public CumulativeDataTransmissionData? GetCumulativeData()
        {
            _semaphore.Wait();
            try
            {
                return new CumulativeDataTransmissionData
                {
                    DataSent = _snapshot.DataSent,
                    DataReceived = _snapshot.DataReceived,
                    AverageDataSent = _snapshot.AverageDataSent,
                    AverageDataReceived = _snapshot.AverageDataReceived,
                    UpstreamThroughputBps = _snapshot.UpstreamThroughputBps,
                    DownstreamThroughputBps = _snapshot.DownstreamThroughputBps,
                    ThroughputBps = _snapshot.ThroughputBps
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }
        #nullable restore
    }

    public class DataTransmissionMetricSnapshot : HttpMetricSnapshot
    {
        [JsonIgnore] public bool StopUpdate { get; set; }

        public DataTransmissionMetricSnapshot(string roundName, Guid iterationId, string iterationName, string httpMethod, string url, string httpVersion)
        {
            RoundName = roundName;
            IterationId = iterationId;
            IterationName = iterationName;
            HttpMethod = httpMethod;
            URL = url;
            HttpVersion = httpVersion;
        }

        public override LPSMetricType MetricType => LPSMetricType.DataTransmission;

        // Wall-clock elapsed for the active lifetime (ms)
        public double TimeElpased { get; private set; }

        // Cumulative totals (never reset)
        public double DataSent { get; private set; }
        public double DataReceived { get; private set; }

        // Per-request averages (lifetime)
        public double AverageDataSent { get; private set; }
        public double AverageDataReceived { get; private set; }

        // Lifetime (wall-clock) throughput
        public double UpstreamThroughputBps { get; private set; }
        public double DownstreamThroughputBps { get; private set; }
        public double ThroughputBps { get; private set; }

        public void UpdateDataSent(double totalDataSent, double averageDataSentPerRequest, double upstreamThroughputBps, double timeElpased)
        {
            if (StopUpdate) return;
            TimeStamp = DateTime.UtcNow;
            DataSent = totalDataSent;
            AverageDataSent = averageDataSentPerRequest;
            UpstreamThroughputBps = upstreamThroughputBps;
            TimeElpased = timeElpased;
        }

        public void UpdateDataReceived(double totalDataReceived, double averageDataReceivedPerRequest, double downstreamThroughputBps, double timeElapsed)
        {
            if (StopUpdate) return;
            TimeStamp = DateTime.UtcNow;
            DataReceived = totalDataReceived;
            AverageDataReceived = averageDataReceivedPerRequest;
            DownstreamThroughputBps = downstreamThroughputBps;
            TimeElpased = timeElapsed;
        }

        public void UpdateAverageBytes(double averageBytesPerSecond, double timeElapsed)
        {
            if (StopUpdate) return;
            TimeStamp = DateTime.UtcNow;
            ThroughputBps = averageBytesPerSecond;
            TimeElpased = timeElapsed;
        }
    }
}
