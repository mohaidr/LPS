using HdrHistogram;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Monitoring.EventSources;
using Microsoft.Extensions.Logging;
using Spectre.Console.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class ThroughputMetricMonitor : IThroughputMetricMonitor
    {
        public LPSMetricType MetricType => LPSMetricType.ConnectionsCount;
        HttpRun _httpRun;
        public HttpRun LPSHttpRun => _httpRun;
        int _activeRequestssCount;
        int _requestsCount;
        int _successfulRequestsCount;
        int _failedRequestsCount;
        ProtectedConnectionDimensionSet _dimensionSet;
        RequestEventSource _eventSource;
        Stopwatch _throughputWatch;
        Timer _timer;
        Domain.Common.Interfaces.ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        CancellationTokenSource _cts;
        private SpinLock _spinLock = new SpinLock();
        public bool IsStopped { get; private set; }
        public bool IsTestStarted { get; private set; }
        public ThroughputMetricMonitor(HttpRun httprun, CancellationTokenSource cts, Domain.Common.Interfaces.ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _httpRun = httprun;
            _dimensionSet = new ProtectedConnectionDimensionSet(_httpRun.Name, _httpRun.LPSHttpRequestProfile.HttpMethod, _httpRun.LPSHttpRequestProfile.URL, _httpRun.LPSHttpRequestProfile.Httpversion);
            _eventSource = RequestEventSource.GetInstance(_httpRun);
            _throughputWatch = new Stopwatch();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _cts = cts;
        }

        private void UpdateMetrics()
        {
            bool lockTaken = false;
            bool isCoolDown = _httpRun.Mode == HttpRun.IterationMode.DCB || _httpRun.Mode == HttpRun.IterationMode.CRB || _httpRun.Mode == HttpRun.IterationMode.CB;
            int cooldownPeriod = isCoolDown ? _httpRun.CoolDownTime.Value : 1;

            if (IsTestStarted)
            {
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    var timeElapsed = _throughputWatch.Elapsed.TotalMilliseconds;
                    var requestsRate = new RequestsRate($"1s", Math.Round((_successfulRequestsCount / (timeElapsed / 1000)), 2));
                    var requestsRatePerCoolDown = new RequestsRate(string.Empty, 0);
                    if (isCoolDown && timeElapsed > cooldownPeriod)
                    {
                        requestsRatePerCoolDown = new RequestsRate($"{cooldownPeriod}ms", Math.Round((_successfulRequestsCount / timeElapsed) * cooldownPeriod, 2));
                    }
                    _dimensionSet.Update(_activeRequestssCount, _requestsCount, _successfulRequestsCount, _failedRequestsCount, timeElapsed, requestsRate, requestsRatePerCoolDown);
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
                }
            }
        }
        private void SchedualMetricsUpdate()
        {
            _timer = new Timer(_ =>
            {
                if (IsTestStarted && !IsStopped)
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

        public IDimensionSet GetDimensionSet()
        {
            return _dimensionSet;
        }

        public TDimensionSet GetDimensionSet<TDimensionSet>() where TDimensionSet : IDimensionSet
        {
            // Check if _dimensionSet is of the requested type TDimensionSet
            if (_dimensionSet is TDimensionSet dimensionSet)
            {
                return dimensionSet;
            }
            else
            {
                _logger?.Log(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSConnectionsMetricMonitor", LPSLoggingLevel.Error);
                throw new InvalidCastException($"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSConnectionsMetricMonitor");
            }
        }

        public string Stringify()
        {
            try
            {
                return SerializationHelper.Serialize(_dimensionSet);
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
        public bool IncreaseConnectionsCount()
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                _dimensionSet.Update(++_activeRequestssCount, ++_requestsCount);
                _eventSource.AddRequest();
                IsTestStarted = true;
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
                UpdateMetrics();
            }
        }

        public bool DecreseConnectionsCount(bool isSuccess)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                if (isSuccess)
                    _dimensionSet.Update(--_activeRequestssCount, _requestsCount, ++_successfulRequestsCount);
                else
                    _dimensionSet.Update(--_activeRequestssCount, _requestsCount, _successfulRequestsCount, ++_failedRequestsCount);

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
                UpdateMetrics();
            }
        }

        public void Start()
        {
            _throughputWatch.Start();
            IsStopped = false;
            _dimensionSet.StopUpdate = false;
            SchedualMetricsUpdate();
        }

        public void Stop()
        {
            IsStopped = true;
            IsTestStarted = false;
            _dimensionSet.StopUpdate = true;
            try
            {
                _throughputWatch.Stop();
                _timer.Dispose();
            }
            finally { }
        }

        private class ProtectedConnectionDimensionSet : ConnectionDimensionSet
        {
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
    public class ConnectionDimensionSet : IDimensionSet
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
