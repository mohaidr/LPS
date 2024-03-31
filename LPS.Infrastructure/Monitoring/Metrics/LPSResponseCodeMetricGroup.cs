using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Spectre.Console;
using System.Collections.Concurrent;
using LPS.Infrastructure.Monitoring.EventSources;
using System.Collections.ObjectModel;
namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSResponseCodeMetricGroup : ILPSResponseMetric
    {
        LPSHttpRun _httpRun;
        LPSResponseMetricEventSource _eventSource;
        private static readonly object _lockObject = new object();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        Guid _groupId;
        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }
        internal LPSResponseCodeMetricGroup(LPSHttpRun httpRun)
        {
            _groupId = Guid.NewGuid();
            _httpRun = httpRun;
            _eventSource = LPSResponseMetricEventSource.GetInstance(_httpRun);
            _dimensionsSet =  new ProtectedResponseCodeDimensionSet();
        }

        public LPSMetricType MetricType => LPSMetricType.ResponseCode;
        private ProtectedResponseCodeDimensionSet _dimensionsSet { get; set; }
        public ILPSResponseMetric Update(LPSHttpResponse response)
        {
            return UpdateAsync(response).Result;
        }
        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {

            await _semaphore.WaitAsync();
            {
                try
                {
                    string endpointDetails = $"{_httpRun.Name} - {_httpRun.LPSHttpRequestProfile.HttpMethod} {_httpRun.LPSHttpRequestProfile.URL} HTTP/{_httpRun.LPSHttpRequestProfile.Httpversion}";
                    _dimensionsSet.update(response, endpointDetails);
                    _eventSource.WriteResponseBreakDownMetrics(response.StatusCode);
                }
                finally
                {

                    _semaphore.Release();
                }

            }
            return this;
        }
        public string Stringify()
        {
            try
            {
                return LPSSerializationHelper.Serialize(_dimensionsSet);

            }
            catch (Exception ex)
            {
                return string.Empty;
            }
        }

        public IDimensionSet GetDimensionSet()
        {
            return _dimensionsSet;
        }

        private class ProtectedResponseCodeDimensionSet: ResponseCodeDimensionSet
        {
            public void update(LPSHttpResponse response, string endpoint) {
                var key = $"{(int)response.StatusCode} {response.StatusMessage}";
                TimeStamp = DateTime.UtcNow;
                ResponseStatusDictionary.AddOrUpdate(key, 1, (k, oldValue) => oldValue + 1);
                TimeStamp = DateTime.Now;
                EndPointDetails = endpoint;
            }
        }
    }

    public class ResponseCodeDimensionSet : IDimensionSet
    {
        public ResponseCodeDimensionSet()
        {
            ResponseStatusDictionary = new ConcurrentDictionary<string, int>();
        }
        public DateTime TimeStamp { get; protected set; }
        public string EndPointDetails { get; protected set; }
        protected ConcurrentDictionary<string, int> ResponseStatusDictionary {get; set;}
        public IReadOnlyDictionary<string, int> ResponseSummary => new ReadOnlyDictionary<string, int>(ResponseStatusDictionary);
    }
}
