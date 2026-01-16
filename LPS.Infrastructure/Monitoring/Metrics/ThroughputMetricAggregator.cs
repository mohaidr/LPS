using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.MetricsServices;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class ThroughputMetricAggregator : BaseMetricAggregator, IThroughputMetricCollector, IDisposable
    {
        private const string MetricName = "Throughput";

        private int _currentActiveRequests;
        private int _maxConcurrentRequests;
        private int _requestsCount;
        private readonly ThroughputMetricSnapshot _snapshot;
        protected override IMetricShapshot Snapshot => _snapshot;

        private readonly Stopwatch _throughputWatch;
        private Timer _timer;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _isStarted;
        private bool _disposed;

        // NEW: metrics variable service
        private readonly IMetricsVariableService _metricsVariableService;
        private readonly IFailureRulesService _failureRulesService;

        public override LPSMetricType MetricType => LPSMetricType.Throughput;
        public readonly string _roundName;
        public int CurrentActiveRequests => _currentActiveRequests;
        public ThroughputMetricAggregator(
            HttpIteration httpIteration,
            string roundName,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService,
            IFailureRulesService failureRulesService,
            ILiveMetricDataStore metricDataStore) : base(httpIteration, logger, runtimeOperationIdProvider, metricDataStore)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
            _failureRulesService = failureRulesService ?? throw new ArgumentNullException(nameof(failureRulesService));
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));

            _snapshot = new ThroughputMetricSnapshot(
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

            int cooldownPeriodMs = isCoolDown ? _httpIteration.CoolDownTime.Value : 1;

            // We later should add a check if not started but now in the current design it causes exceptions or logical errors 


            try
            {
                int successCount = 0;
                int failedCount = 0;
                if (_metricDataStore.TryGetLatest(_httpIteration.Id, LPSMetricType.ResponseCode, out ResponseCodeMetricSnapshot snapshot))
                {
                    // Calculate success/failed based on error status codes from failure rules
                    foreach (var summary in snapshot.ResponseSummaries)
                    {
                        int statusCode = (int)summary.HttpStatusCode;
                        if (_failureRulesService.IsErrorStatusCode(_httpIteration, _roundName, statusCode))
                        {
                            failedCount += summary.Count;
                        }
                        else
                        {
                            successCount += summary.Count;
                        }
                    }
                }

                var timeElapsed = _throughputWatch.Elapsed.TotalMilliseconds;
                var requestsRate = new RequestsRate(string.Empty, 0);
                var requestsRatePerCoolDown = new RequestsRate(string.Empty, 0);

                if (timeElapsed > 1000)
                {
                    requestsRate = new RequestsRate("1s", Math.Round((successCount / (timeElapsed / 1000)), 2));
                }
                if (isCoolDown && timeElapsed > cooldownPeriodMs)
                {
                    requestsRatePerCoolDown = new RequestsRate($"{cooldownPeriodMs}ms",
                        Math.Round((successCount / timeElapsed) * cooldownPeriodMs, 2));
                }

                _snapshot.Update(
                    _maxConcurrentRequests,
                    _currentActiveRequests,
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
        private void ScheduleMetricsUpdate()
        {
            _timer?.Dispose(); // To avoid multuple timers running at the same time.
            _timer = new Timer(_ =>
            {
                if (!_isStarted || _disposed) return;

                try
                {
                    // Serialize with the rest of the aggregator operations
                    _semaphore.Wait();

                    // Fully synchronous inside the callback to avoid async-void pitfalls:
                    UpdateMetricsAsync(CancellationToken.None)
                        .AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId,
                        $"Failed to update throughput metrics\n{ex}", LPSLoggingLevel.Error);
                }
                finally
                {
                    // Ensure release even if UpdateMetricsAsync throws
                    if (_semaphore.CurrentCount == 0)
                        _semaphore.Release();
                }
            }, state: null, dueTime: 0, period: 1000); // first tick after 1s is fine
        }

        /// <summary>
        /// Lazy start: starts the stopwatch and timer on first call.
        /// Called internally when first request is recorded.
        /// </summary>
        private void EnsureStarted()
        {
            if (_isStarted || _disposed) return;
            _isStarted = true;
            _throughputWatch.Start();
            _snapshot.StopUpdate = false;
            ScheduleMetricsUpdate();
        }

        public async ValueTask<bool> IncreaseConnectionsCount(CancellationToken token)
        {
            bool isLockTaken = false;
            try
            {
                await _semaphore.WaitAsync(token);
                isLockTaken = true;
                
                // Lazy start on first request
                EnsureStarted();
                
                ++_currentActiveRequests;
                if (_currentActiveRequests > _maxConcurrentRequests)
                {
                    _maxConcurrentRequests = _currentActiveRequests;
                }
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
                await _semaphore.WaitAsync(token); // Keep the is lock taken as a best practice - do not move the await _semaphore.WaitAsync(token); before the try immediatly and remove the isLockTaken
                isLockTaken = true;
                --_currentActiveRequests;
                await UpdateMetricsAsync(token);
                return true;
            }
            finally
            {
                if (isLockTaken)
                    _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _snapshot.StopUpdate = true;
            _throughputWatch.Stop();
            _timer?.Dispose();
            _timer = null;
            _semaphore.Dispose();
        }

        /// <summary>
        /// Gets the current cumulative throughput data for the CumulativeIterationMetricsCollector.
        /// Similar to how WindowedThroughputAggregator.GetWindowDataAndReset() works, but does NOT reset.
        /// </summary>
        #nullable enable
        public CumulativeThroughputData? GetCumulativeData(out string? targetUrl)
        {
            targetUrl = null;
            _semaphore.Wait();
            try
            {
                targetUrl = _snapshot.URL;
                return new CumulativeThroughputData
                {
                    RequestsCount = _snapshot.RequestsCount,
                    SuccessfulRequestCount = _snapshot.SuccessfulRequestCount,
                    FailedRequestsCount = _snapshot.FailedRequestsCount,
                    MaxConcurrentRequests = _snapshot.MaxConcurrentRequests,
                    RequestsPerSecond = _snapshot.RequestsRate.Value,
                    RequestsRatePerCoolDown = _snapshot.RequestsRatePerCoolDownPeriod.Value,
                    ErrorRate = _snapshot.ErrorRate,
                    TimeElapsedMs = _snapshot.TimeElapsed
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }
        #nullable restore

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
        [JsonIgnore]
        public bool StopUpdate { get; set; }

        public ThroughputMetricSnapshot(string roundName, Guid iterationId, string iterationName, string httpMethod, string url, string httpVersion)
        {
            IterationId = iterationId;
            RoundName = roundName;
            IterationName = iterationName;
            HttpMethod = httpMethod;
            URL = url;
            HttpVersion = httpVersion;
        }

        public override LPSMetricType MetricType => LPSMetricType.Throughput;

        // Cumulative metrics (never reset)
        public double TimeElapsed { get; private set; }
        public RequestsRate RequestsRate { get; private set; }
        public RequestsRate RequestsRatePerCoolDownPeriod { get; private set; }
        public int RequestsCount { get; private set; }
        public int MaxConcurrentRequests { get; private set; }
        public int CurrentActiveRequests { get; private set; }
        public int SuccessfulRequestCount { get; private set; }
        public int FailedRequestsCount { get; private set; }
        public double ErrorRate => FailedRequestsCount / (SuccessfulRequestCount + FailedRequestsCount != 0 ? (double)(SuccessfulRequestCount + FailedRequestsCount) : 1);

        public void Update(
        int maxConcurrentRequests,
        int currentActiveRequests,
        int requestsCount = default,
        int successfulRequestsCount = default,
        int failedRequestsCount = default,
        double timeElpased = default,
        RequestsRate requestsRate = default,
        RequestsRate requestsRatePerCoolDown = default)
        {
            if (!StopUpdate)
            {
                TimeStamp = DateTime.UtcNow;
                
                // Update cumulative
                RequestsCount = requestsCount.Equals(default) ? RequestsCount : requestsCount;
                MaxConcurrentRequests = maxConcurrentRequests;
                CurrentActiveRequests = currentActiveRequests;
                SuccessfulRequestCount = successfulRequestsCount.Equals(default) ? SuccessfulRequestCount : successfulRequestsCount;
                FailedRequestsCount = failedRequestsCount.Equals(default) ? FailedRequestsCount : failedRequestsCount;
                TimeElapsed = timeElpased.Equals(default) ? TimeElapsed : timeElpased;
                RequestsRate = requestsRate.Equals(default(RequestsRate)) ? RequestsRate : requestsRate;
                RequestsRatePerCoolDownPeriod = requestsRatePerCoolDown.Equals(default(RequestsRate))
                    ? RequestsRatePerCoolDownPeriod
                    : requestsRatePerCoolDown;
            }
        }
    }
}