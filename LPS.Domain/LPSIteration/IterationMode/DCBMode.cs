using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class DCBMode : IIterationModeService
    {
        private readonly HttpRequest.ExecuteCommand _command;
        private readonly int _duration;
        private readonly int _coolDownTime;
        private readonly int _batchSize;
        private readonly bool _maximizeThroughput;
        private readonly IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> _batchProcessor;
        readonly HttpIteration _httpIteration;
        readonly IIterationStatusMonitor _iterationStatusMonitor;
        readonly IWatchdog _watchdog;
        private readonly IMetricsDataMonitor _metrics;

        public DCBMode(
            HttpRequest.ExecuteCommand command,
            int duration,
            int coolDownTime,
            int batchSize,
            bool maximizeThroughput,
            IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> batchProcessor,
            HttpIteration httpIteration,
            IIterationStatusMonitor iterationStatusMonitor,
            IWatchdog watchdog,
            IMetricsDataMonitor metrics = null)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
            _duration = duration;
            _coolDownTime = coolDownTime;
            _batchSize = batchSize;
            _maximizeThroughput = maximizeThroughput;
            _httpIteration = httpIteration ?? throw new ArgumentNullException(nameof(httpIteration));
            _iterationStatusMonitor = iterationStatusMonitor ?? throw new ArgumentNullException(nameof(iterationStatusMonitor));
            _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
            _metrics = metrics;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            var awaitableTasks = new BatchTaskTracker();
            Exception executionError = null;

            var stopwatch = Stopwatch.StartNew();
            var coolDownWatch = Stopwatch.StartNew();

            bool continueCondition() => stopwatch.Elapsed.TotalSeconds < _duration && !cancellationToken.IsCancellationRequested;
            Func<bool> batchCondition = continueCondition;
            bool newBatch = true;

            IDisposable cooling = null;
            try
            {
                while (continueCondition() && !await _iterationStatusMonitor.IsTerminatedAsync(_httpIteration, cancellationToken))
                {
                    if (_maximizeThroughput)
                    {
                        if (newBatch)
                        {
                            cooling?.Dispose();
                            cooling = null;
                            coolDownWatch.Restart();
                            await Task.Yield();
                            await _watchdog.BalanceAsync(_httpIteration.HttpRequest.Url.HostName, cancellationToken);
                            awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition, cancellationToken));
                            if (continueCondition() && coolDownWatch.Elapsed.TotalMilliseconds < _coolDownTime)
                                cooling = _metrics?.BeginBatchCooldown(_httpIteration);
                        }
                        newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= _coolDownTime;
                    }
                    else
                    {
                        coolDownWatch.Restart();
                        await _watchdog.BalanceAsync(_httpIteration.HttpRequest.Url.HostName, cancellationToken);
                        awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition, cancellationToken));
                        if (continueCondition())
                        {
                            using var pause = _metrics?.BeginBatchCooldown(_httpIteration);
                            await Task.Delay((int)Math.Max(_coolDownTime, _coolDownTime - coolDownWatch.ElapsedMilliseconds), cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                executionError = ex;
            }
            finally
            {
                cooling?.Dispose();
            }

            coolDownWatch.Stop();
            stopwatch.Stop();

            return await awaitableTasks.CompleteAsync(executionError);
        }
    }
}
