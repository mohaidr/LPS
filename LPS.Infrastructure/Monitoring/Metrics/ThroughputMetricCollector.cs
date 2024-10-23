using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.EventSources;
using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class ThroughputMetricCollector : BaseMetricCollector, IThroughputMetricCollector
    {
        int _activeRequestssCount;
        int _requestsCount;
        int _successfulRequestsCount;
        int _failedRequestsCount;
        ProtectedConnectionDimensionSet _dimensionSet;
        protected override IDimensionSet DimensionSet => _dimensionSet;

        public override LPSMetricType MetricType => LPSMetricType.Throughput;

        readonly RequestEventSource _eventSource;
        readonly Stopwatch _throughputWatch;
        Timer _timer;
        private SpinLock _spinLock = new();
        private SpinLock _elapsedSpinLock = new();
        public ThroughputMetricCollector(HttpRun httprun, Domain.Common.Interfaces.ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider) : base(httprun, logger, runtimeOperationIdProvider)
        {
            _httpRun = httprun;
            _dimensionSet = new ProtectedConnectionDimensionSet(_httpRun.Name, _httpRun.LPSHttpRequestProfile.HttpMethod, _httpRun.LPSHttpRequestProfile.URL, _httpRun.LPSHttpRequestProfile.Httpversion);
            _eventSource = RequestEventSource.GetInstance(_httpRun);
            _throughputWatch = new Stopwatch();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        readonly object lockObject = new();
        private void UpdateMetrics()
        {
            bool isCoolDown = _httpRun.Mode == IterationMode.DCB || _httpRun.Mode == IterationMode.CRB || _httpRun.Mode == IterationMode.CB;
            int cooldownPeriod = isCoolDown ? _httpRun.CoolDownTime.Value : 1;

            if (!IsStopped)
            {
                try
                {
                    lock (lockObject)
                    {
                        var timeElapsed = _throughputWatch.Elapsed.TotalMilliseconds;
                        var requestsRate = new RequestsRate(string.Empty, 0);
                        var requestsRatePerCoolDown = new RequestsRate(string.Empty, 0);

                        if (timeElapsed > 1000)
                        {
                            requestsRate = new RequestsRate($"1s", Math.Round((_successfulRequestsCount / (timeElapsed / 1000)), 2));
                        }
                        if (isCoolDown && timeElapsed > cooldownPeriod)
                        {
                            requestsRatePerCoolDown = new RequestsRate($"{cooldownPeriod}ms", Math.Round((_successfulRequestsCount / timeElapsed) * cooldownPeriod, 2));
                        }
                        _dimensionSet.Update(_activeRequestssCount, _requestsCount, _successfulRequestsCount, _failedRequestsCount, timeElapsed, requestsRate, requestsRatePerCoolDown);
                    }
                }
                finally
                {
                }
            }
        }

        // A timer is necessary for periods of inactivity while the test is still running, such as during a watchdog check or the time between the start and completion of a request, etc.
        private void SchedualMetricsUpdate()
        {
            _timer = new Timer(_ =>
            {
                if (!IsStopped)
                {
                    try
                    {
                        UpdateMetrics();
                    }
                    finally
                    {
                    }
                }
            }, null, 0, 1000);

        }

        public bool IncreaseConnectionsCount()
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                ++_activeRequestssCount;
                ++_requestsCount;
                _eventSource.AddRequest();
                UpdateMetrics();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        public bool DecreseConnectionsCount(bool isSuccess)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                if (isSuccess)
                {
                    --_activeRequestssCount;
                    ++_successfulRequestsCount;
                }
                else
                {
                    --_activeRequestssCount;
                    ++_failedRequestsCount;
                }

                UpdateMetrics();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        public override void Start()
        {
            _throughputWatch.Start();
            IsStopped = false;
            _dimensionSet.StopUpdate = false;
            SchedualMetricsUpdate();
        }

        public override void Stop()
        {
            IsStopped = true;
            _dimensionSet.StopUpdate = true;
            try
            {
                _throughputWatch.Stop();
                _timer.Dispose();
            }
            finally { }
        }

        private class ProtectedConnectionDimensionSet : ThroughputDimensionSet
        {
            [JsonIgnore]
            public bool StopUpdate { get; set; }
            public ProtectedConnectionDimensionSet(string name, string httpMethod, string url, string httpVersion)
            {
                RunName = name;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }
            // When calling this method, make sure you take thread safety into considration
            public void Update(int activeRequestsCount, int requestsCount = default, int successfulRequestsCount = default, int failedRequestsCount = default, double timeElapsedInSeconds = default, RequestsRate requestsRate = default, RequestsRate requestsRatePerCoolDown = default)
            {
                if (!StopUpdate)
                {
                    TimeStamp = DateTime.UtcNow;
                    this.RequestsCount = requestsCount.Equals(default) ? this.RequestsCount : requestsCount;
                    this.ActiveRequestsCount = activeRequestsCount;
                    this.SuccessfulRequestCount = successfulRequestsCount.Equals(default) ? this.SuccessfulRequestCount : successfulRequestsCount;
                    this.FailedRequestsCount = failedRequestsCount.Equals(default) ? this.FailedRequestsCount : failedRequestsCount;
                    this.TimeElapsedInSeconds = timeElapsedInSeconds.Equals(default) ? this.TimeElapsedInSeconds : timeElapsedInSeconds;
                    this.RequestsRate = requestsRate.Equals(default(RequestsRate)) ? this.RequestsRate : requestsRate;
                    this.RequestsRatePerCoolDownPeriod = requestsRatePerCoolDown.Equals(default(RequestsRate)) ? this.RequestsRatePerCoolDownPeriod : requestsRatePerCoolDown;
                }
            }
        }
    }

    public readonly struct RequestsRate(string every, double value) : IEquatable<RequestsRate>
    {
        public double Value { get; } = value;
        public string Every { get; } = every;
        public bool Equals(RequestsRate other)
        {
            return Value.Equals(other.Value) && string.Equals(Every, other.Every, StringComparison.Ordinal);
        }
        public override bool Equals(object obj)
        {
            return obj is RequestsRate other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Value, Every);
        }
        public static bool operator ==(RequestsRate left, RequestsRate right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(RequestsRate left, RequestsRate right)
        {
            return !(left == right);
        }
        public override string ToString()
        {
            return $"RequestsRate: Every = {Every}, Value = {Value}";
        }
    }
    public class ThroughputDimensionSet : IDimensionSet
    {
        public DateTime TimeStamp { get; protected set; }
        public string RunName { get; protected set; }
        public string URL { get; protected set; }
        public string HttpMethod { get; protected set; }
        public string HttpVersion { get; protected set; }
        public int RequestsCount { get; protected set; }
        public int ActiveRequestsCount { get; protected set; }
        public int SuccessfulRequestCount { get; protected set; }
        public int FailedRequestsCount { get; protected set; }
        public double TimeElapsedInSeconds { get; protected set; }
        public RequestsRate RequestsRate { get; protected set; }
        public RequestsRate RequestsRatePerCoolDownPeriod { get; protected set; }
    }
}
