using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class DataTransmissionMetricCollector : BaseMetricCollector, IDataTransmissionMetricCollector
    {
        private SpinLock _spinLock = new();
        private readonly string _roundName;
        private double _totalDataSent = 0;
        private double _totalDataReceived = 0;
        private int _requestsCount = 0;
        private double _totalDataUploadTimeSeconds = 0;
        private double _totalDataTransmissionSeconds = 0;
        private double _totalDataDownloadTimeSeconds = 0;
        private LPSDurationMetricDimensionSetProtected _dimensionSet;
        IMetricsQueryService _metricsQueryService;
        internal DataTransmissionMetricCollector(HttpIteration httpIteration, string roundName, IMetricsQueryService metricsQueryService, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            : base(httpIteration, logger, runtimeOperationIdProvider)
        {
            _roundName = roundName;
            _httpIteration = httpIteration;
            _dimensionSet = new LPSDurationMetricDimensionSetProtected(_roundName, httpIteration.Id, httpIteration.Name, httpIteration.HttpRequest.HttpMethod, httpIteration.HttpRequest.Url.Url, httpIteration.HttpRequest.HttpVersion);
            _logger = logger;
            _metricsQueryService = metricsQueryService;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
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

        public void UpdateDataSent(double totalBytes, double elapsedTicks, CancellationToken token = default)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                _totalDataUploadTimeSeconds += elapsedTicks / Stopwatch.Frequency; //(elapsedTicks/Stopwatch.Frequency) => gives the time in second
                if (!IsStarted)
                {
                    throw new InvalidOperationException("Metric collector is stopped.");
                }

                // Update the total and count, then calculate the average
                _totalDataSent += totalBytes;
                _requestsCount = _metricsQueryService.GetAsync<ThroughputMetricCollector>(m => m.HttpIteration.Id == this._dimensionSet.IterationId).Result
                    .Single()
                    .GetDimensionSetAsync<ThroughputDimensionSet>().Result
                    .RequestsCount;
                UpdateMetrics();
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        public void UpdateDataReceived(double totalBytes, double elapsedTicks, CancellationToken token = default)
        {

            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                if (!IsStarted)
                {
                    throw new InvalidOperationException("Metric collector is stopped.");
                }

                // Update the total and count, then calculate the average
                _totalDataDownloadTimeSeconds += elapsedTicks / Stopwatch.Frequency; // (elapsedTicks/Stopwatch.Frequency) => gives the time in second
                _totalDataReceived += totalBytes;
                _requestsCount = _metricsQueryService.GetAsync<ThroughputMetricCollector>(m => m.HttpIteration.Id == this._dimensionSet.IterationId).Result
                    .Single()
                    .GetDimensionSetAsync<ThroughputDimensionSet>().Result
                    .RequestsCount;

                UpdateMetrics();
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        readonly object lockObject = new();

        private void UpdateMetrics()
        {
            try
            {
                lock (lockObject)
                {
                    _totalDataTransmissionSeconds = _totalDataDownloadTimeSeconds + _totalDataUploadTimeSeconds;
                    _dimensionSet.UpdateDataSent(_totalDataSent, _requestsCount > 0 ? _totalDataSent / _requestsCount : 0, _totalDataUploadTimeSeconds > 0 ? _totalDataSent / _totalDataUploadTimeSeconds : 0, _totalDataTransmissionSeconds * 1000);
                    _dimensionSet.UpdateDataReceived(_totalDataReceived, _requestsCount > 0 ? _totalDataReceived / _requestsCount : 0, _totalDataDownloadTimeSeconds > 0 ? _totalDataReceived / _totalDataDownloadTimeSeconds : 0, _totalDataTransmissionSeconds * 1000);
                    _dimensionSet.UpdateAverageBytes(_totalDataTransmissionSeconds > 0 ? (_totalDataReceived + _totalDataSent) / _totalDataTransmissionSeconds : 0, _totalDataTransmissionSeconds * 1000);
                }
            }
            finally
            {

            }
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

    public class DataTransmissionMetricDimensionSet : IHttpDimensionSet
    {

        [JsonIgnore]
        public DateTime TimeStamp { get; protected set; }
        public double TotalDataTransmissionTimeInMilliseconds { get; protected set; }
        [JsonIgnore]
        public string RoundName { get; protected set; }
        [JsonIgnore]
        public Guid IterationId { get; protected set; }
        [JsonIgnore]
        public string IterationName { get; protected set; }
        [JsonIgnore]
        public string URL { get; protected set; }
        [JsonIgnore]
        public string HttpMethod { get; protected set; }
        [JsonIgnore]
        public string HttpVersion { get; protected set; }
        public double DataSent { get; protected set; }
        public double DataReceived { get; protected set; }
        public double AverageDataSent { get; protected set; }
        public double AverageDataReceived { get; protected set; }
        public double UpstreamThroughputBps { get; protected set; }
        public double DownstreamThroughputBps { get; protected set; }
        public double ThroughputBps { get; protected set; }
    }
}
