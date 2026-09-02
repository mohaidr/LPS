using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.EventSources;
using LPS.Infrastructure.Monitoring.MetricsVariables;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using LPS.Infrastructure.Monitoring.MetricsServices;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class ResponseCodeMetricAggregator : BaseMetricAggregator, IResponseMetricCollector, IDisposable
    {

        private const string MetricName = "ResponseCode";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ResponseMetricEventSource _eventSource;
        private ResponseCodeMetricSnapshot _snapshot { get; set; }

        // NEW: metrics variable service
        private readonly IMetricsVariableService _metricsVariableService;
        private readonly string _roundName;
        private readonly int _publishIntervalMs;
        private Timer _timer;
        private bool _isStarted;
        private bool _disposed;
        internal ResponseCodeMetricAggregator(
            HttpIteration httpIteration,
            string roundName,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService // NEW
        , ILiveMetricDataStore metricDataStore,
            LiveMetricsPublishingOptions publishingOptions) : base(httpIteration, logger, runtimeOperationIdProvider, metricDataStore)
        {
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _eventSource = ResponseMetricEventSource.GetInstance(_httpIteration);
            _roundName = roundName ?? throw new ArgumentNullException(nameof(roundName));
            _snapshot = new ResponseCodeMetricSnapshot(
                roundName,
                _httpIteration.Id,
                _httpIteration.Name,
                _httpIteration.HttpRequest.HttpMethod,
                _httpIteration.HttpRequest.Url.Url,
                _httpIteration.HttpRequest.HttpVersion);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
            ArgumentNullException.ThrowIfNull(publishingOptions);
            _publishIntervalMs = publishingOptions.PublishIntervalMs;
            PushMetricAsync(_snapshot.CreateCopy(), default).Wait();
        }

        protected override IMetricShapshot Snapshot => _snapshot;

        public override LPSMetricType MetricType => LPSMetricType.ResponseCode;

        public IResponseMetricCollector Update(HttpResponse.SetupCommand response, CancellationToken token)
            => UpdateAsync(response, token).Result;

        public async Task<IResponseMetricCollector> UpdateAsync(HttpResponse.SetupCommand response, CancellationToken token)
        {
            bool isLockTaken;
            await _semaphore.WaitAsync(token);
            isLockTaken = true;
            try
            {
                EnsureStarted();
                _snapshot.Update(response);
                _eventSource.WriteResponseBreakDownMetrics(response.StatusCode);
            }
            finally
            {
                if (isLockTaken) _semaphore.Release();
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
                    $"Failed to publish response-code metrics\n{ex}", LPSLoggingLevel.Error);
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
        /// Gets the current cumulative response code data for the CumulativeIterationMetricsCollector.
        /// Similar to how WindowedResponseCodeAggregator.GetWindowDataAndReset() works, but does NOT reset.
        /// </summary>
        #nullable enable
        public CumulativeResponseCodeData? GetCumulativeData()
        {
            _semaphore.Wait();
            try
            {
                return new CumulativeResponseCodeData
                {
                    ResponseSummaries = _snapshot.ResponseSummaries
                        .Select(s => new CumulativeResponseSummary
                        {
                            HttpStatusCode = (int)s.HttpStatusCode,
                            HttpStatusReason = s.HttpStatusReason,
                            Count = s.Count
                        })
                        .ToList()
                };
            }
            finally
            {
                _semaphore.Release();
            }
        }
        #nullable restore

        // NEW: serialize and publish the dimension set to the variable system
        private async Task PushMetricAsync(ResponseCodeMetricSnapshot snapshot, CancellationToken token)
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

    public class HttpResponseSummary(HttpStatusCode httpStatusCode, string httpStatusReason, int count)
    {
        public HttpStatusCode HttpStatusCode { get; private set; } = httpStatusCode;
        public string HttpStatusReason { get; private set; } = httpStatusReason;
        public int Count { get; set; } = count;
    }

    public class ResponseCodeMetricSnapshot : HttpMetricSnapshot
    {
        public ResponseCodeMetricSnapshot(string roundName, Guid iterationId, string iterationName, string httpMethod, string url, string httpVersion)
        {
            IterationId = iterationId;
            RoundName = roundName;
            IterationName = iterationName;
            HttpMethod = httpMethod;
            URL = url;
            HttpVersion = httpVersion;
            _responseSummaries = new ConcurrentBag<HttpResponseSummary>();
        }

        public void Update(HttpResponse.SetupCommand response)
        {
            var summary = _responseSummaries.FirstOrDefault(rs =>
                rs.HttpStatusCode == response.StatusCode &&
                rs.HttpStatusReason == response.StatusMessage);

            if (summary != null)
            {
                summary.Count += 1;
            }
            else
            {
                var instance = new HttpResponseSummary(
                    response.StatusCode,
                    response.StatusMessage,
                    1
                );
                _responseSummaries.Add(instance);
            }

            TimeStamp = DateTime.UtcNow;
        }

        public ResponseCodeMetricSnapshot CreateCopy()
        {
            var copy = new ResponseCodeMetricSnapshot(RoundName, IterationId, IterationName, HttpMethod, URL, HttpVersion)
            {
                TimeStamp = TimeStamp
            };

            foreach (var summary in _responseSummaries)
            {
                copy._responseSummaries.Add(new HttpResponseSummary(
                    summary.HttpStatusCode,
                    summary.HttpStatusReason,
                    summary.Count));
            }

            return copy;
        }

        public override LPSMetricType MetricType => LPSMetricType.ResponseCode;

        protected ConcurrentBag<HttpResponseSummary> _responseSummaries { get; private set; }

        public IList<HttpResponseSummary> ResponseSummaries => _responseSummaries.ToList();
    }
}