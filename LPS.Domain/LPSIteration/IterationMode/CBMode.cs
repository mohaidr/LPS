using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class CBMode : IIterationModeService
    {
        private readonly HttpRequest.ExecuteCommand _command;
        private readonly int _coolDownTime;
        private readonly int _batchSize;
        private readonly bool _maximizeThroughput;
        private readonly IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> _batchProcessor;
        readonly HttpIteration _httpIteration;
        readonly IIterationStatusMonitor _iterationStatusMonitor;
        readonly IWatchdog _watchdog;
        public CBMode(
            HttpRequest.ExecuteCommand command,
            int coolDownTime,
            int batchSize,
            bool maximizeThroughput,
            IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> batchProcessor, 
            HttpIteration httpIteration,
            IIterationStatusMonitor iterationStatusMonitor,
            IWatchdog watchdog)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _coolDownTime = coolDownTime;
            _batchSize = batchSize;
            _maximizeThroughput = maximizeThroughput;
            _batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
            _httpIteration = httpIteration ?? throw new ArgumentNullException();
            _iterationStatusMonitor = iterationStatusMonitor ?? throw new ArgumentNullException();
            _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
        }

        public async Task<int> ExecuteAsync(CancellationToken token)
        {
            var coolDownWatch = Stopwatch.StartNew();
            List<Task<int>> awaitableTasks = new();

            bool continueCondition() => !token.IsCancellationRequested;
            Func<bool> batchCondition = continueCondition;
            bool newBatch = true;

            while (continueCondition() && !await _iterationStatusMonitor.IsTerminatedAsync(_httpIteration, token))
            {
                if (_maximizeThroughput)
                {
                    if (newBatch)
                    {
                        coolDownWatch.Restart();
                        await Task.Yield();
                        await _watchdog.BalanceAsync(_httpIteration.HttpRequest.Url.HostName, token);
                        awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition, token));
                    }
                    newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= _coolDownTime;
                }
                else
                {
                    coolDownWatch.Restart();
                    await _watchdog.BalanceAsync(_httpIteration.HttpRequest.Url.HostName, token);
                    awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition, token));
                    await Task.Delay((int)Math.Max(_coolDownTime, _coolDownTime - coolDownWatch.ElapsedMilliseconds), token);
                }
            }

            coolDownWatch.Stop();
            try
            {
                var results = await Task.WhenAll(awaitableTasks);
                return results.Sum();
            }
            catch
            {
                throw;
            }
        }
    }
}
