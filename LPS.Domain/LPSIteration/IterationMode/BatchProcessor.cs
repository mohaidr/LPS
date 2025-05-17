using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class BatchProcessor: IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest>
    {
        readonly HttpRequest _request;
        readonly HttpIteration _httpIteration;
        readonly IWatchdog _watchdog;
        readonly string _hostName;
        public BatchProcessor(HttpIteration httpIteration, HttpRequest request,
            IWatchdog watchdog) 
        {
            _request = request;
            _watchdog = watchdog;
            _httpIteration = httpIteration;
            _hostName = _request.Url.HostName;
        }
        public async Task<int> SendBatchAsync(HttpRequest.ExecuteCommand command, int batchSize, Func<bool> batchCondition, CancellationToken token)
        {
            try
            {
                List<Task> awaitableTasks = [];
                int _numberOfSentRequests = 0;
                for (int b = 0; b < batchSize && batchCondition(); b++)
                {
                    await _watchdog.BalanceAsync(_hostName, token);
                    awaitableTasks.Add(command.ExecuteAsync(_request));
                    _numberOfSentRequests++;
                }
                await Task.WhenAll(awaitableTasks);
                return _numberOfSentRequests;
            }
            catch (Exception) {
                throw;
            }
        }
    }
}
