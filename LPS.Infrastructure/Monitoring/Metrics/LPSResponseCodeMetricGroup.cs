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
using LPS.Domain.Common.Interfaces;
using System.Diagnostics;
using System.Timers;
namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSResponseCodeMetricGroup : ILPSResponseMetric
    {
        LPSHttpRun _httpRun;
        LPSResponseMetricEventSource _eventSource;
        private static readonly object _lockObject = new object();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }
        ILPSLogger _logger;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;

        internal LPSResponseCodeMetricGroup(LPSHttpRun httpRun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider = default)
        {
            _httpRun = httpRun;
            _eventSource = LPSResponseMetricEventSource.GetInstance(_httpRun);
            _dimensionSet =  new ProtectedResponseCodeDimensionSet();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public LPSMetricType MetricType => LPSMetricType.ResponseCode;
        private ProtectedResponseCodeDimensionSet _dimensionSet { get; set; }
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
                    _dimensionSet.update(response, endpointDetails);
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
                return LPSSerializationHelper.Serialize(_dimensionSet);

            }
            catch (Exception ex)
            {
                return string.Empty;
            }
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
                _logger?.Log(_runtimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSResponseCodeMetricGroup", LPSLoggingLevel.Error);
                throw new InvalidCastException($"Dimension set of type {typeof(TDimensionSet)} is not supported by the LPSResponseCodeMetricGroup");
            }
        }

        public void Dispose()
        {
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
