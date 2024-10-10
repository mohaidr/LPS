//using HdrHistogram;
//using LPS.Domain;
//using LPS.Infrastructure.Common;
//using LPS.Infrastructure.Common.Interfaces;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using Spectre.Console;
//using System.Diagnostics.Tracing;
//using LPS.Infrastructure.Monitoring.EventSources;
//using LPS.Domain.Common.Interfaces;
//using System.Diagnostics;
//using System.Timers;
//using System.Text.Json.Serialization;
//using System.Net;
//namespace LPS.Infrastructure.Monitoring.Metrics
//{

//    public class DataTransmissionMetricCollector : BaseMetricCollector, IDataTransmissionMetricCollector
//    {
//        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
//        private LPSDurationMetricDimensionSetProtected _dimensionSet { get; set; }
//        internal DataTransmissionMetricCollector(HttpRun httpRun, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider) : base (httpRun, logger, runtimeOperationIdProvider)
//        {
//            _httpRun = httpRun;
//            _dimensionSet = new LPSDurationMetricDimensionSetProtected(httpRun.Name, httpRun.LPSHttpRequestProfile.HttpMethod, httpRun.LPSHttpRequestProfile.URL, httpRun.LPSHttpRequestProfile.Httpversion);
//            _logger = logger;
//            _runtimeOperationIdProvider = runtimeOperationIdProvider;
//        }
//        protected override IDimensionSet DimensionSet => _dimensionSet;

//        public override LPSMetricType MetricType => LPSMetricType.DataTransmission;


//        public override void Stop()
//        {
//            IsStopped = true;
//        }
//        public override void Start()
//        {
//            IsStopped = false;
//        }

//        private class LPSDurationMetricDimensionSetProtected : LPSDataTransmissionMetricDimensionSet
//        {
//            public LPSDurationMetricDimensionSetProtected(string name, string httpMethod, string url, string httpVersion) {
//                RunName = name;
//                HttpMethod = httpMethod;
//                URL = url;
//                HttpVersion = httpVersion;
//            }
//            public void Update(double dataSent, LongHistogram histogram)
//            {
//                double averageDenominator = AverageResponseTime != 0 ? (SumResponseTime / AverageResponseTime) + 1 : 1;
//                TimeStamp = DateTime.UtcNow;
//                MaxResponseTime = Math.Max(responseTime, MaxResponseTime);
//                MinResponseTime = MinResponseTime == 0 ? responseTime : Math.Min(responseTime, MinResponseTime);
//                SumResponseTime = SumResponseTime + responseTime;
//                AverageResponseTime = SumResponseTime / averageDenominator;
//                histogram.RecordValue((long)responseTime);
//                P10ResponseTime = histogram.GetValueAtPercentile(10);
//                P50ResponseTime = histogram.GetValueAtPercentile(50);
//                P90ResponseTime = histogram.GetValueAtPercentile(90);
//            }
//        }
//    }

//    public class LPSDataTransmissionMetricDimensionSet : IDimensionSet
//    {
//        public DateTime TimeStamp { get; protected set; }
//        public string RunName { get; protected set; }
//        public string URL { get; protected set; }
//        public string HttpMethod { get; protected set; }
//        public string HttpVersion { get; protected set; }
//        public double SumResponseTime { get; protected set; }
//        public double DataSent { get; protected set; }
//        public double DataReceived { get; protected set; }
//        public double AverageDataSent { get; protected set; }
//        public double AverageDataReceived { get; protected set; }

//    }
//}
