using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.MetricsServices;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class ThroughputMetricAggregator : BaseMetricAggregator, IThroughputMetricCollector
    {
        private const string MetricName = "Throughput";

        private int _activeRequestssCount;
        private int _requestsCount;
        private readonly ProtectedConnectionSnapshot _snapshot;
        protected override IMetricShapshot Snapshot => _snapshot;

        private readonly Stopwatch _throughputWatch;
        private Timer _timer;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        // NEW: metrics variable service
        private readonly IMetricsVariableService _metricsVariableService;

        public override LPSMetricType MetricType => LPSMetricType.Throughput;
        public readonly string _roundName;
        public ThroughputMetricAggregator(
            HttpIteration httpIteration,
            string roundName,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService // NEW
        , IMetricDataStore metricDataStore) : base(httpIteration, logger, runtimeOperationIdProvider, metricDataStore)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));

            _snapshot = new ProtectedConnectionSnapshot(
                roundName,
                _httpIteration.Id,
                _httpIteration.Name,
                _httpIteration.HttpRequest.HttpMethod,
                _httpIteration.HttpRequest.Url.Url,
                _httpIteration.HttpRequest.HttpVersion);
            _throughputWatch = new Stopwatch();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            PushMetricAsync(default).Wait();
        }

        private async ValueTask<bool> UpdateMetricsAsync(CancellationToken token)
        {
            bool isCoolDown = _httpIteration.Mode == IterationMode.DCB
                           || _httpIteration.Mode == IterationMode.CRB
                           || _httpIteration.Mode == IterationMode.CB;

            int cooldownPeriod = isCoolDown ? _httpIteration.CoolDownTime.Value : 1;

            if (!IsStarted)
                return false;

            try
            {
                int successCount = 0;
                int failedCount = 0;
                if (_metricDataStore.TryGetLatest(_httpIteration.Id, LPSMetricType.ResponseCode, out ResponseCodeMetricSnapshot snapshot))
                {
                    successCount = snapshot
                        .ResponseSummaries.Where(r => !HttpIteration.ErrorStatusCodes.Contains(r.HttpStatusCode))
                        .Sum(r => r.Count);

                    failedCount = snapshot
                        .ResponseSummaries.Where(r => HttpIteration.ErrorStatusCodes.Contains(r.HttpStatusCode))
                        .Sum(r => r.Count);
                }

                var timeElapsed = _throughputWatch.Elapsed.TotalMilliseconds;
                var requestsRate = new RequestsRate(string.Empty, 0);
                var requestsRatePerCoolDown = new RequestsRate(string.Empty, 0);

                if (timeElapsed > 1000)
                {
                    requestsRate = new RequestsRate("1s", Math.Round((successCount / (timeElapsed / 1000)), 2));
                }
                if (isCoolDown && timeElapsed > cooldownPeriod)
                {
                    requestsRatePerCoolDown = new RequestsRate($"{cooldownPeriod}ms",
                        Math.Round((successCount / timeElapsed) * cooldownPeriod, 2));
                }

                _snapshot.Update(
                    _activeRequestssCount,
                    _requestsCount,
                    successCount,
                    failedCount,
                    timeElapsed,
                    requestsRate,
                    requestsRatePerCoolDown);

                await PushMetricAsync(token); // NEW
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Failed to update throughput metrics \n{ex}", LPSLoggingLevel.Error, token);
                return false;
            }
        }

        // A timer is necessary for periods of inactivity while the test is still running
        private void SchedualMetricsUpdate()
        {
            _timer = new Timer(_ =>
            {
                if (IsStarted)
                {
                    try
                    {
                        _semaphore.Wait();
                        _ = UpdateMetricsAsync(default).Result;
                        _semaphore.Release();
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(_runtimeOperationIdProvider.OperationId,
                            $"Failed to update throughput metrics \n{ex}", LPSLoggingLevel.Error);
                    }
                }
            }, null, 0, 1000);
        }

        public async ValueTask<bool> IncreaseConnectionsCount(CancellationToken token)
        {
            bool isLockTaken = false;
            try
            {
                await _semaphore.WaitAsync(token);
                isLockTaken = true;
                ++_activeRequestssCount;
                ++_requestsCount;
                await UpdateMetricsAsync(token);
                return true;
            }
            finally
            {
                if (isLockTaken)
                    _semaphore.Release();
            }
        }

        public async ValueTask<bool> DecreseConnectionsCount(CancellationToken token)
        {
            bool isLockTaken = false;
            try
            {
                await _semaphore.WaitAsync(token);
                isLockTaken = true;
                --_activeRequestssCount;
                await UpdateMetricsAsync(token);
                return true;
            }
            finally
            {
                if (isLockTaken)
                    _semaphore.Release();
            }
        }

        public override void Start()
        {
            if (!IsStarted)
            {
                IsStarted = true;
                _throughputWatch.Start();
                _snapshot.StopUpdate = false;
                SchedualMetricsUpdate();
            }
        }

        public override void Stop()
        {
            if (IsStarted)
            {
                IsStarted = false;
                _snapshot.StopUpdate = true;
                try
                {
                    _throughputWatch?.Stop();
                    _timer?.Dispose();
                }
                finally { }
            }
        }

        // NEW: Serialize and push to Metrics variable system
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

        private class ProtectedConnectionSnapshot : ThroughputMetricSnapshot
        {
            [JsonIgnore]
            public bool StopUpdate { get; set; }

            public ProtectedConnectionSnapshot(string roundName, Guid iterationId, string iterationName, string httpMethod, string url, string httpVersion)
            {
                IterationId = iterationId;
                RoundName = roundName;
                IterationName = iterationName;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }

            public void Update(
                int activeRequestsCount,
                int requestsCount = default,
                int successfulRequestsCount = default,
                int failedRequestsCount = default,
                double totalDataTransmissionTimeInMilliseconds = default,
                RequestsRate requestsRate = default,
                RequestsRate requestsRatePerCoolDown = default)
            {
                if (!StopUpdate)
                {
                    TimeStamp = DateTime.UtcNow;
                    RequestsCount = requestsCount.Equals(default) ? RequestsCount : requestsCount;
                    ActiveRequestsCount = activeRequestsCount;
                    SuccessfulRequestCount = successfulRequestsCount.Equals(default) ? SuccessfulRequestCount : successfulRequestsCount;
                    FailedRequestsCount = failedRequestsCount.Equals(default) ? FailedRequestsCount : failedRequestsCount;
                    TotalDataTransmissionTimeInMilliseconds = totalDataTransmissionTimeInMilliseconds.Equals(default)
                        ? TotalDataTransmissionTimeInMilliseconds
                        : totalDataTransmissionTimeInMilliseconds;
                    RequestsRate = requestsRate.Equals(default(RequestsRate)) ? RequestsRate : requestsRate;
                    RequestsRatePerCoolDownPeriod = requestsRatePerCoolDown.Equals(default(RequestsRate))
                        ? RequestsRatePerCoolDownPeriod
                        : requestsRatePerCoolDown;
                }
            }
        }
    }

    public readonly struct RequestsRate(string every, double value) : IEquatable<RequestsRate>
    {
        public double Value { get; } = value;
        public string Every { get; } = every;
        public bool Equals(RequestsRate other) =>
            Value.Equals(other.Value) && string.Equals(Every, other.Every, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is RequestsRate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Value, Every);
        public static bool operator ==(RequestsRate left, RequestsRate right) => left.Equals(right);
        public static bool operator !=(RequestsRate left, RequestsRate right) => !(left == right);
        public override string ToString() => $"RequestsRate: Every = {Every}, Value = {Value}";
    }

    public class ThroughputMetricSnapshot : HttpMetricSnapshot
    {
        public override LPSMetricType MetricType => LPSMetricType.Throughput;

        public double TotalDataTransmissionTimeInMilliseconds { get; protected set; }
        public RequestsRate RequestsRate { get; protected set; }
        public RequestsRate RequestsRatePerCoolDownPeriod { get; protected set; }
        public int RequestsCount { get; protected set; }
        public int ActiveRequestsCount { get; protected set; }
        public int SuccessfulRequestCount { get; protected set; }
        public int FailedRequestsCount { get; protected set; }
        public double ErrorRate => FailedRequestsCount / (RequestsCount - ActiveRequestsCount != 0 ? (double)RequestsCount - ActiveRequestsCount : 1);
    }
}