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
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class LPSConnectionsMetricGroup : ILPSConnectionsMetric

    {
        public LPSMetricType MetricType => LPSMetricType.ConnectionsCount;
        LPSHttpRun _httpRun;
        public LPSHttpRun LPSHttpRun => _httpRun;
        int _activeRequestssCount;
        int _requestsCount;
        double _completedRequestsCount;
        ProtectedConnectionDimensionSet _dimensionSet;
        LPSRequestEventSource _eventSource;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        Stopwatch _stopwatch;
        string _endpointDetails;
        Timer _timer;
        ILPSLogger _logger;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public LPSConnectionsMetricGroup(LPSHttpRun httprun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider = default)
        {
            _httpRun = httprun;
            _dimensionSet = new ProtectedConnectionDimensionSet();
            _eventSource = LPSRequestEventSource.GetInstance(_httpRun);
            _stopwatch = new Stopwatch();
            _endpointDetails = $"{_httpRun.Name} - {_httpRun.LPSHttpRequestProfile.HttpMethod} {_httpRun.LPSHttpRequestProfile.URL} HTTP/{_httpRun.LPSHttpRequestProfile.Httpversion}";
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            SchedualMetricsUpdate();
        }
        private void SchedualMetricsUpdate()
        {
            bool isCoolDown = _httpRun.Mode == LPSHttpRun.IterationMode.DCB || _httpRun.Mode == LPSHttpRun.IterationMode.CRB || _httpRun.Mode == LPSHttpRun.IterationMode.CB;
            bool isDurationOrRequest = _httpRun.Mode == LPSHttpRun.IterationMode.D || _httpRun.Mode == LPSHttpRun.IterationMode.R;
            int sleep = isCoolDown ? _httpRun.CoolDownTime.Value : 1;
            _stopwatch.Start();
            _timer = new Timer(_ =>
            {
                var timeElapsed = Math.Round(_stopwatch.Elapsed.TotalSeconds, 2);
                var requestsRate = new RequestsRate($"{sleep}s", Math.Round((_completedRequestsCount / timeElapsed) *sleep, 2));
                _dimensionSet.Update(_activeRequestssCount, _requestsCount, _completedRequestsCount, timeElapsed, requestsRate, _endpointDetails);
            }, null, 0, sleep);
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
                _logger?.Log(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSConnectionsMetricGroup", LPSLoggingLevel.Error);
                throw new InvalidCastException($"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSConnectionsMetricGroup");
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
        public async Task<bool> IncreaseConnectionsCountAsync(ICancellationTokenWrapper cancellationTokenWrapper)
        {
            try
            {
                await _semaphore.WaitAsync();

                double currentElapsed = _stopwatch.Elapsed.TotalSeconds;
                ++_activeRequestssCount;
                ++_requestsCount;
                _eventSource.AddRequest();
                return true;
            }
            catch (Exception ex)
            { 
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> DecreseConnectionsCountAsync(ICancellationTokenWrapper cancellationTokenWrapper)
        {
            try
            {
                await _semaphore.WaitAsync();
                ++_completedRequestsCount;
                --_activeRequestssCount;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _timer.Dispose();
        }



        private class ProtectedConnectionDimensionSet : ConnectionDimensionSet
        {
            // When calling this method, make sure you take thread safety into considration
            public void Update(int? activeRequestsCount, int? requestsCount = default, double? completedRequestsCount = default, double? timeElapsedInSeconds = default, RequestsRate? requestsRate = default, string endPointDetails = default)
            {
                TimeStamp = DateTime.Now;
                this.RequestsCount = requestsCount ?? this.RequestsCount;
                this.ActiveRequestsCount = activeRequestsCount ?? this.ActiveRequestsCount;
                this.CompletedRequestsCount = completedRequestsCount ?? this.CompletedRequestsCount;
                this.TimeElapsedInSeconds = timeElapsedInSeconds ?? this.TimeElapsedInSeconds;
                this.RequestsRate = requestsRate ?? this.RequestsRate;
                this.EndPointDetails = endPointDetails == default ? this.EndPointDetails: endPointDetails;
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
        public string Every { get;}
    }
    public class ConnectionDimensionSet : IDimensionSet
    {
        public static DateTime TimeStamp { get; protected set; }
        public string EndPointDetails { get; protected set; }
        public int RequestsCount { get; protected set; }
        public int ActiveRequestsCount { get; protected set; }
        public double CompletedRequestsCount { get; protected set; }
        public double TimeElapsedInSeconds { get; protected set; }
        public RequestsRate RequestsRate { get; protected set; }
    }
}
