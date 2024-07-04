using HdrHistogram;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Monitoring.EventSources;
using Microsoft.Extensions.Logging;
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
    public class LPSConnectionsMetricMonitor : ILPSConnectionsMetricMonitor

    {
        public LPSMetricType MetricType => LPSMetricType.ConnectionsCount;
        LPSHttpRun _httpRun;
        public LPSHttpRun LPSHttpRun => _httpRun;
        int _activeRequestssCount;
        int _requestsCount;
        int _successfulRequestsCount;
        int _failedRequestsCount;
        ProtectedConnectionDimensionSet _dimensionSet;
        LPSRequestEventSource _eventSource;
        Stopwatch _stopwatch;
        Timer _timer;
        ILPSLogger _logger;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private SpinLock _spinLock = new SpinLock();
        public bool IsStopped { get; private set; }
        public LPSConnectionsMetricMonitor(LPSHttpRun httprun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider = default)
        {
            _httpRun = httprun;
            _dimensionSet = new ProtectedConnectionDimensionSet(_httpRun.Name, _httpRun.LPSHttpRequestProfile.HttpMethod, _httpRun.LPSHttpRequestProfile.URL, _httpRun.LPSHttpRequestProfile.Httpversion);
            _eventSource = LPSRequestEventSource.GetInstance(_httpRun);
            _stopwatch = new Stopwatch();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        private void SchedualMetricsUpdate()
        {
            bool isCoolDown = _httpRun.Mode == LPSHttpRun.IterationMode.DCB || _httpRun.Mode == LPSHttpRun.IterationMode.CRB || _httpRun.Mode == LPSHttpRun.IterationMode.CB;
            bool isDurationOrRequest = _httpRun.Mode == LPSHttpRun.IterationMode.D || _httpRun.Mode == LPSHttpRun.IterationMode.R;
            int cooldownPeriod = isCoolDown ? _httpRun.CoolDownTime.Value : 1;
            _stopwatch.Start();
            _timer = new Timer(_ =>
            {
                bool lockTaken = false;
                try
                {

                    var timeElapsed = _stopwatch.Elapsed.TotalSeconds;
                    var requestsRate = new RequestsRate($"1s", Math.Round((_successfulRequestsCount / timeElapsed), 2));
                    var requestsRatePerCoolDown = new RequestsRate(string.Empty, 0);
                    if (isCoolDown && timeElapsed > cooldownPeriod)
                    {
                        requestsRatePerCoolDown = new RequestsRate($"{cooldownPeriod}s", Math.Round((_successfulRequestsCount / timeElapsed) * cooldownPeriod, 2));
                    }
                    _spinLock.Enter(ref lockTaken);
                    _dimensionSet.Update(_activeRequestssCount, _requestsCount, _successfulRequestsCount, _failedRequestsCount, timeElapsed, requestsRate, requestsRatePerCoolDown);
                }
                finally
                {
                    if (lockTaken)
                        _spinLock.Exit();
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
                return LPSSerializationHelper.Serialize(_dimensionSet);
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }
        public bool IncreaseConnectionsCount(ICancellationTokenWrapper cancellationTokenWrapper)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                _dimensionSet.Update(++_activeRequestssCount, ++_requestsCount);
                _eventSource.AddRequest();
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

        public bool DecreseConnectionsCount(bool isSuccess, ICancellationTokenWrapper cancellationTokenWrapper)
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
            }
        }

        public void Start()
        {
            IsStopped = false;
            SchedualMetricsUpdate();
        }

        public void Stop()
        {
            try
            {
                _stopwatch.Stop();
                _timer.Dispose();
            }
            finally
            {
                IsStopped = true;
            }

        }

        private class ProtectedConnectionDimensionSet : ConnectionDimensionSet
        {
            public ProtectedConnectionDimensionSet(string name, string httpMethod, string url, string httpVersion)
            {
                RunName = name;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }
            // When calling this method, make sure you take thread safety into considration
            public void Update(int activeRequestsCount , int requestsCount = default, int successfulRequestsCount = default, int failedRequestsCount = default, double timeElapsedInSeconds = default, RequestsRate requestsRate = default, RequestsRate requestsRatePerCoolDown = default)
            {
                TimeStamp = DateTime.Now;
                this.RequestsCount = requestsCount.Equals(default) ? this.RequestsCount : requestsCount;
                this.ActiveRequestsCount =  activeRequestsCount;
                this.SuccessfulRequestCount = successfulRequestsCount.Equals(default) ? this.SuccessfulRequestCount : successfulRequestsCount;
                this.FailedRequestsCount = failedRequestsCount.Equals(default) ? this.FailedRequestsCount : failedRequestsCount;
                this.TimeElapsedInSeconds = timeElapsedInSeconds.Equals(default) ? this.TimeElapsedInSeconds : timeElapsedInSeconds;
                this.RequestsRate = requestsRate.Equals(default(RequestsRate)) ? this.RequestsRate : requestsRate;
                this.RequestsRatePerCoolDownPeriod = requestsRatePerCoolDown.Equals(default(RequestsRate)) ? this.RequestsRatePerCoolDownPeriod : requestsRatePerCoolDown;
            }
        }

    }

    public struct RequestsRate
    {
        public RequestsRate(string every, double value)
        {
            Value = value;
            Every = every;
        }
        public double Value { get; }
        public string Every { get; }
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
