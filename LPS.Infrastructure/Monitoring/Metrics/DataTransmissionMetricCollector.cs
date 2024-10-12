using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class DataTransmissionMetricCollector : BaseMetricCollector, IDataTransmissionMetricCollector
    {
        private SpinLock _spinLock = new SpinLock();
        private double _totalDataSent = 0;
        private double _totalDataReceived = 0;
        private int _dataSentCount = 0;
        private int _dataReceivedCount = 0;
        private LPSDurationMetricDimensionSetProtected _dimensionSet;

        internal DataTransmissionMetricCollector(HttpRun httpRun, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            : base(httpRun, logger, runtimeOperationIdProvider)
        {
            _httpRun = httpRun;
            _dimensionSet = new LPSDurationMetricDimensionSetProtected(httpRun.Name, httpRun.LPSHttpRequestProfile.HttpMethod, httpRun.LPSHttpRequestProfile.URL, httpRun.LPSHttpRequestProfile.Httpversion);
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        protected override IDimensionSet DimensionSet => _dimensionSet;

        public override LPSMetricType MetricType => LPSMetricType.DataTransmission;

        public override void Start()
        {
            IsStopped = false;
            _logger.LogAsync("Start", "DataTransmissionMetricCollector started.", LPSLoggingLevel.Verbose).ConfigureAwait(false);
        }

        public override void Stop()
        {
            IsStopped = true;
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
                _dimensionSet.UpdateDataSent(_totalDataSent, _totalDataSent / _dataSentCount);

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
                _dimensionSet.UpdateDataReceived(_totalDataReceived, _totalDataReceived / _dataReceivedCount);

            }
            finally
            {
                if (lockTaken)
                    _spinLock.Exit();
            }
        }

        private class LPSDurationMetricDimensionSetProtected : LPSDataTransmissionMetricDimensionSet
        {
            public LPSDurationMetricDimensionSetProtected(string name, string httpMethod, string url, string httpVersion)
            {
                RunName = name;
                HttpMethod = httpMethod;
                URL = url;
                HttpVersion = httpVersion;
            }

            public void UpdateDataSent(double totalDataSent, double averageDataSent)
            {
                TimeStamp = DateTime.UtcNow;
                DataSent = totalDataSent;
                AverageDataSent = averageDataSent;
            }

            public void UpdateDataReceived(double totalDataReceived, double averageDataReceived)
            {
                TimeStamp = DateTime.UtcNow;
                DataReceived = totalDataReceived;
                AverageDataReceived = averageDataReceived;
            }
        }
    }

    public class LPSDataTransmissionMetricDimensionSet : IDimensionSet
    {
        public DateTime TimeStamp { get; protected set; }
        public string RunName { get; protected set; }
        public string URL { get; protected set; }
        public string HttpMethod { get; protected set; }
        public string HttpVersion { get; protected set; }
        public double DataSent { get; protected set; }
        public double DataReceived { get; protected set; }
        public double AverageDataSent { get; protected set; }
        public double AverageDataReceived { get; protected set; }
    }
}
