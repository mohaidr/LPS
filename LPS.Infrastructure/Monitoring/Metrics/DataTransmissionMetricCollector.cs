using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Diagnostics;
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
        private int _dataSentCount = 0;
        private int _dataReceivedCount = 0;
        private LPSDurationMetricDimensionSetProtected _dimensionSet;
        private Timer _timer;
        readonly Stopwatch _dataTransmissionWatch;

        internal DataTransmissionMetricCollector(HttpIteration httpIteration, string roundName, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            : base(httpIteration, logger, runtimeOperationIdProvider)
        {
            _roundName = roundName;
            _httpIteration = httpIteration;
            _dimensionSet = new LPSDurationMetricDimensionSetProtected(_roundName, httpIteration.Id, httpIteration.Name, httpIteration.Session.HttpMethod, httpIteration.Session.URL, httpIteration.Session.HttpVersion);
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _dataTransmissionWatch = new Stopwatch();
        }

        protected override IDimensionSet DimensionSet => _dimensionSet;

        public override LPSMetricType MetricType => LPSMetricType.DataTransmission;
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
        public override void Start()
        {
            _dataTransmissionWatch.Start();
            IsStopped = false;
            SchedualMetricsUpdate();
            _logger.LogAsync("Start", "DataTransmissionMetricCollector started.", LPSLoggingLevel.Verbose).ConfigureAwait(false);
        }

        public override void Stop()
        {
            IsStopped = true;
            try
            {
                _dataTransmissionWatch.Stop();
                _timer.Dispose();
            }
            finally { }
            _logger.LogAsync("Stop", "DataTransmissionMetricCollector stopped.", LPSLoggingLevel.Verbose).ConfigureAwait(false);
        }

        public void UpdateDataSentAsync(double dataSize, CancellationToken token = default)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                if (IsStopped)
                {
                    throw new InvalidOperationException("Metric collector is stopped.");
                }

                // Update the total and count, then calculate the average
                _totalDataSent += dataSize;
                _dataSentCount++;
                UpdateMetrics();
            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        public void UpdateDataReceivedAsync(double dataSize, CancellationToken token = default)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                if (IsStopped)
                {
                    throw new InvalidOperationException("Metric collector is stopped.");
                }

                // Update the total and count, then calculate the average
                _totalDataReceived += dataSize;
                _dataReceivedCount++;
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
                    var totalSeconds = _dataTransmissionWatch.Elapsed.TotalSeconds;
                    if (totalSeconds > 0 && _dataSentCount > 0 && _dataReceivedCount > 0)
                    {
                        _dimensionSet.UpdateDataSent(_totalDataSent, _totalDataSent / _dataSentCount, _totalDataSent / totalSeconds, totalSeconds*1000);
                        _dimensionSet.UpdateDataReceived(_totalDataReceived, _totalDataReceived / _dataReceivedCount, _totalDataReceived / totalSeconds, totalSeconds * 1000);
                        _dimensionSet.UpdateAverageBytes((_totalDataReceived + _totalDataSent) / totalSeconds, totalSeconds * 1000);
                    }
                }
            }
            finally
            {

            }
        }

        private class LPSDurationMetricDimensionSetProtected : LPSDataTransmissionMetricDimensionSet
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

            public void UpdateDataSent(double totalDataSent, double averageDataSent, double averageDataSentPerSecond, double timeElapsedInMilliseconds)
            {
                TimeStamp = DateTime.UtcNow;
                DataSent = totalDataSent;
                AverageDataSent = averageDataSent;
                AverageDataSentPerSecond = averageDataSentPerSecond;
                TimeElapsedInMilliseconds = timeElapsedInMilliseconds;
            }

            public void UpdateDataReceived(double totalDataReceived, double averageDataReceived, double averageDataReceivedPerSecond, double timeElapsedInMilliseconds)
            {
                TimeStamp = DateTime.UtcNow;
                DataReceived = totalDataReceived;
                AverageDataReceived = averageDataReceived;
                AverageDataReceivedPerSecond = averageDataReceivedPerSecond;
                TimeElapsedInMilliseconds = timeElapsedInMilliseconds;
            }

            public void UpdateAverageBytes(double averageBytesPerSecond, double timeElapsedInMilliseconds)
            {
                TimeStamp = DateTime.UtcNow;
                AverageBytesPerSecond = averageBytesPerSecond;
                TimeElapsedInMilliseconds = timeElapsedInMilliseconds;
            }

        }
    }

    public class LPSDataTransmissionMetricDimensionSet : IHttpDimensionSet
    {

        [JsonIgnore]
        public DateTime TimeStamp { get; protected set; }
        public double TimeElapsedInMilliseconds { get; protected set; }
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
        public double AverageDataSentPerSecond { get; protected set; }
        public double AverageDataReceivedPerSecond { get; protected set; }
        public double AverageBytesPerSecond { get; protected set; }
    }
}
