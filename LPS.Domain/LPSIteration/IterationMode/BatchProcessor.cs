using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class BatchProcessor: IBatchProcessor<HttpSession.ExecuteCommand, HttpSession>
    {
        readonly HttpSession _session;
        readonly IWatchdog _watchdog;
        readonly string _hostName;
        public BatchProcessor(HttpSession session,
            IWatchdog watchdog ) 
        {
            _session = session;
            _watchdog = watchdog;
            _hostName = new Uri(_session.URL).Host;
        }
        public async Task<int> SendBatchAsync(HttpSession.ExecuteCommand command, int batchSize, Func<bool> batchCondition, CancellationToken token)
        {
            try
            {
                List<Task> awaitableTasks = [];
                int _numberOfSentRequests = 0;
                for (int b = 0; b < batchSize && batchCondition(); b++)
                {
                    await _watchdog.BalanceAsync(_hostName, token);
                    awaitableTasks.Add(command.ExecuteAsync(_session));
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
