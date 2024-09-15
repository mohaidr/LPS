using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class BatchProcessor: IBatchProcessor<HttpRequestProfile.ExecuteCommand, HttpRequestProfile>
    {
        HttpRequestProfile _requestProfile;
        IWatchdog _watchdog;
        string _hostName;
        public BatchProcessor(HttpRequestProfile requestProfile,
            IWatchdog watchdog ) 
        {
            _requestProfile = requestProfile;
            _watchdog = watchdog;
            _hostName = new Uri(_requestProfile.URL).Host;
        }
        public async Task<int> SendBatchAsync(HttpRequestProfile.ExecuteCommand command, int batchSize, Func<bool> batchCondition)
        {
            int _numberOfSentRequests = 0; 
            for (int b = 0; b < batchSize && batchCondition(); b++)
            {
                await _watchdog.BalanceAsync(_hostName);
                _ = command.ExecuteAsync(_requestProfile);
                _numberOfSentRequests++;
            }
            return _numberOfSentRequests;
        }
    }
}
