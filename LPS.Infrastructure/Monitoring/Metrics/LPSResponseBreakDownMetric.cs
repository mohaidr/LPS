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
namespace LPS.Infrastructure.Monitoring.Metrics
{

    public class LPSResponseBreakDownMetric : ILPSResponseMetric
    {
        LPSHttpRun _httpRun;
        ConsoleWriter.ConsoleWriter _consoleWriter;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public LPSHttpRun LPSHttpRun { get { return _httpRun; } }

        internal LPSResponseBreakDownMetric(LPSHttpRun httpRun)
        {
            _httpRun = httpRun;
            _dimensionsList = new List<ResponseDimensionSet>();
            _consoleWriter = new ConsoleWriter.ConsoleWriter(1,1);
        }
        public ResponseMetricType MetricType => ResponseMetricType.ResponseBreakDown;
        private List<ResponseDimensionSet> _dimensionsList { get; set; }
        List<ResponseDimensionSet> DimensionsList { get { return _dimensionsList; } }
        public ILPSResponseMetric Update(LPSHttpResponse response)
        {
            return UpdateAsync(response).Result;
        }
        public async Task<ILPSResponseMetric> UpdateAsync(LPSHttpResponse response)
        {
            await _semaphore.WaitAsync();
            try
            {
                var existingDimension = _dimensionsList.Find(d => d.StatusCode == response.StatusCode && d.StatusReason == response.StatusMessage);

                if (existingDimension != null)
                {
                    // If dimension exists, increment the count
                    existingDimension.Sum++;
                }
                else
                {
                    // If dimension doesn't exist, add a new one
                    _dimensionsList.Add(new ResponseDimensionSet
                    {
                        StatusCode = response.StatusCode,
                        StatusReason = response.StatusMessage,
                        Sum = 1
                    });
                }
                _consoleWriter.AddMessage(this.Stringify(), 5, 1, ConsoleColor.White);
            }
            finally
            {
                _semaphore.Release();
            }
            return this;
        }
        public string Stringify()
        {
            try 
            {
                return LPSSerializationHelper.Serialize(DimensionsList);
 
            }
            catch (InvalidOperationException ex) {
                Console.WriteLine(ex.Message);
                return string.Empty;
            }    
        }
    }

    internal class ResponseDimensionSet
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusReason { get; set; }
        public int Sum { get; set; }
    }
}
