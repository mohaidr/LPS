using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.MetricsVariables; // <-- add
using LPS.Infrastructure.VariableServices.VariableHolders;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class DataTransmissionMetricCollector : BaseMetricCollector, IDataTransmissionMetricCollector
    {
        private const string MetricName = "DataTransmission";

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly string _roundName;
        private double _totalDataSent = 0;
        private double _totalDataReceived = 0;
        private int _requestsCount = 0;
        private double _totalDataUploadTimeSeconds = 0;
        private double _totalDataTransmissionSeconds = 0;
        private double _totalDataDownloadTimeSeconds = 0;
        private readonly LPSDurationMetricDimensionSetProtected _dimensionSet;
        private readonly IMetricsQueryService _metricsQueryService;

        // NEW: metrics variable service
        private readonly IMetricsVariableService _metricsVariableService;

        internal DataTransmissionMetricCollector(
            HttpIteration httpIteration,
            string roundName,
            IMetricsQueryService metricsQueryService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService) // <-- add
            : base(httpIteration, logger, runtimeOperationIdProvider)
        {
            _roundName = roundName;
            _httpIteration = httpIteration;
            _dimensionSet = new LPSDurationMetricDimensionSetProtected(
                _roundName,
                httpIteration.Id,
                httpIteration.Name,
                httpIteration.HttpRequest.HttpMethod,
                httpIteration.HttpRequest.Url.Url,
                httpIteration.HttpRequest.HttpVersion);

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metricsQueryService = metricsQueryService ?? throw new ArgumentNullException(nameof(metricsQueryService));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVariableService = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
        }

        protected override IDimensionSet DimensionSet => _dimensionSet;

        public override LPSMetricType MetricType => LPSMetricType.DataTransmission;

        public override void Start()
        {
            if (!IsStarted)
            {
                IsStarted = true;
                _logger.LogAsync("Start", "DataTransmissionMetricCollector started.", LPSLoggingLevel.Verbose).ConfigureAwait(false);
            }
        }

        public override void Stop()
        {
            if (IsStarted)
            {
                IsStarted = false;
                try
                {
                    _logger.LogAsync("Stop", "DataTransmissionMetricCollector stopped.", LPSLoggingLevel.Verbose).ConfigureAwait(false);
                }
                finally { }
            }
        }

        public async ValueTask UpdateDataSentAsync(double totalBytes, double elapsedTicks, CancellationToken token = default)
        {
            bool lockTaken = false;
            try
            {
                await _semaphore.WaitAsync(token);
                lockTaken = true;

                if (!IsStarted) throw new InvalidOperationException("Metric collector is stopped.");
                _totalDataUploadTimeSeconds += elapsedTicks / Stopwatch.Frequency;
                _totalDataSent += totalBytes;
                _requestsCount = await GetRequestsCountAsync(token);

                await UpdateMetricsAsync(token); // <-- pass token
            }
            finally
            {
                if (lockTaken) _semaphore.Release();
            }
        }

        public async ValueTask UpdateDataReceivedAsync(double totalBytes, double elapsedTicks, CancellationToken token = default)
        {
            bool lockTaken = false;
            try
            {
                await _semaphore.WaitAsync(token);
                lockTaken = true;
                if (!IsStarted) throw new InvalidOperationException("Metric collector is stopped.");

                _totalDataDownloadTimeSeconds += elapsedTicks / Stopwatch.Frequency;
                _totalDataReceived += totalBytes;
                _requestsCount = await GetRequestsCountAsync(token);

                await UpdateMetricsAsync(token); // <-- pass token
            }
            finally
            {
                if (lockTaken) _semaphore.Release();
            }
        }

        // UPDATED: accept token and push to Metrics variable service
        private async ValueTask UpdateMetricsAsync(CancellationToken token)
        {
            try
            {
                _totalDataTransmissionSeconds = _totalDataDownloadTimeSeconds + _totalDataUploadTimeSeconds;

                _dimensionSet.UpdateDataSent(
                    _totalDataSent,
                    _requestsCount > 0 ? _totalDataSent / _requestsCount : 0,
                    _totalDataUploadTimeSeconds > 0 ? _totalDataSent / _totalDataUploadTimeSeconds : 0,
                    _totalDataTransmissionSeconds * 1000);

                _dimensionSet.UpdateDataReceived(
                    _totalDataReceived,
                    _requestsCount > 0 ? _totalDataReceived / _requestsCount : 0,
                    _totalDataDownloadTimeSeconds > 0 ? _totalDataReceived / _totalDataDownloadTimeSeconds : 0,
                    _totalDataTransmissionSeconds * 1000);

                _dimensionSet.UpdateAverageBytes(
                    _totalDataTransmissionSeconds > 0 ? (_totalDataReceived + _totalDataSent) / _totalDataTransmissionSeconds : 0,
                    _totalDataTransmissionSeconds * 1000);

                // Serialize the dimension set and publish to variable system
                var json = JsonSerializer.Serialize(_dimensionSet, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false
                });

                // Push under: Metrics.{IterationName}.DataTransmission
                await _metricsVariableService.PutMetricAsync(_httpIteration.Name, MetricName, json, token);
            }
            finally { }
        }

        private async Task<int> GetRequestsCountAsync(CancellationToken token)
        {
            var throughputCollectors = await _metricsQueryService
                .GetAsync<ThroughputMetricCollector>(m => m.HttpIteration.Id == _dimensionSet.IterationId, token);

            var single = throughputCollectors.Single();

            var dim = await single.GetDimensionSetAsync<ThroughputMetricDimensionSet>(token);
            return dim.RequestsCount;
        }
        private class LPSDurationMetricDimensionSetProtected : DataTransmissionMetricDimensionSet
        {
            public LPSDurationMetricDimensionSetProtected(string roundName, Guid iterationId, string iterationName, string httpMethod, string url, string httpVersion)
            {
                RoundName = roundName;
                IterationId = iterationId;
                IterationName = iterationName;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }

            public void UpdateDataSent(double totalDataSent, double averageDataSentPerRequest, double averageDataSentPerSecond, double totalDataTransmissionTimeInMilliseconds)
            {
                TimeStamp = DateTime.UtcNow;
                DataSent = totalDataSent;
                AverageDataSent = averageDataSentPerRequest;
                UpstreamThroughputBps = averageDataSentPerSecond;
                TotalDataTransmissionTimeInMilliseconds = totalDataTransmissionTimeInMilliseconds;
            }

            public void UpdateDataReceived(double totalDataReceived, double averageDataReceivedPerRequest, double averageDataReceivedPerSecond, double totalDataTransmissionTimeInMilliseconds)
            {
                TimeStamp = DateTime.UtcNow;
                DataReceived = totalDataReceived;
                AverageDataReceived = averageDataReceivedPerRequest;
                DownstreamThroughputBps = averageDataReceivedPerSecond;
                TotalDataTransmissionTimeInMilliseconds = totalDataTransmissionTimeInMilliseconds;
            }

            public void UpdateAverageBytes(double averageBytesPerSecond, double totalDataTransmissionTimeInMilliseconds)
            {
                TimeStamp = DateTime.UtcNow;
                ThroughputBps = averageBytesPerSecond;
                TotalDataTransmissionTimeInMilliseconds = totalDataTransmissionTimeInMilliseconds;
            }
        }
    }

    public class DataTransmissionMetricDimensionSet : HttpMetricDimensionSet
    {
        public double TotalDataTransmissionTimeInMilliseconds { get; protected set; }
        public double DataSent { get; protected set; }
        public double DataReceived { get; protected set; }
        public double AverageDataSent { get; protected set; }
        public double AverageDataReceived { get; protected set; }
        public double UpstreamThroughputBps { get; protected set; }
        public double DownstreamThroughputBps { get; protected set; }
        public double ThroughputBps { get; protected set; }
    }
}
