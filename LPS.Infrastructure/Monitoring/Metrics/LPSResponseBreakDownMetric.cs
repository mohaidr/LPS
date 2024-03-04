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
namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSResponseBreakDownMetric : ILPSResponseMetric
    {
        LPSHttpRun _httpRun;
        private static readonly object _lockObject = new object();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        Guid _groupId;
        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }
        internal LPSResponseBreakDownMetric(LPSHttpRun httpRun)
        {
            _groupId = Guid.NewGuid();
            _httpRun = httpRun;
            _dimensionsList = new List<ResponseDimensionSet>();
        }

        public ResponseMetricType MetricType => ResponseMetricType.ResponseBreakDown;
        private List<ResponseDimensionSet> _dimensionsList { get; set; }
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
                    var existingDimension = _dimensionsList.Find(d => d.StatusCode == response.StatusCode && d.StatusReason == response.StatusMessage);

                    if (existingDimension != null)
                    {
                        int sum = existingDimension.Sum;
                        existingDimension.Sum = Interlocked.Increment(ref sum);
                    }
                    else
                    {
                        // If dimension doesn't exist, add a new one
                        _dimensionsList.Add(new ResponseDimensionSet
                        {
                            StatusCode = response.StatusCode,
                            StatusReason = response.StatusMessage,
                            Sum = 1,
                            EndPointDetails = $"{_httpRun.Name} - {_httpRun.LPSHttpRequestProfile.HttpMethod} {_httpRun.LPSHttpRequestProfile.URL} HTTP/{_httpRun.LPSHttpRequestProfile.Httpversion}"

                        });
                    }
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
                return LPSSerializationHelper.Serialize(_dimensionsList);

            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                return string.Empty;
            }
        }
    }

    internal class ResponseDimensionSet
    {
        public string EndPointDetails { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public string StatusReason { get; set; }
        public int Sum { get; set; }
    }
}
