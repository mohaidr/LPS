using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class CRBMode : IIterationModeService
    {
        private int _requestCount;
        private readonly HttpRequest.ExecuteCommand _command;
        private readonly int _coolDownTime;
        private readonly int _batchSize;
        private readonly bool _maximizeThroughput;
        private readonly IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> _batchProcessor;
        readonly HttpIteration _httpIteration;
        readonly IIterationStatusMonitor _iterationStatusMonitor;
        readonly IWatchdog _watchdog;
        private readonly IMetricsDataMonitor _metrics;

        public CRBMode(
            HttpRequest.ExecuteCommand command,
            int requestCount,
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
            _requestCount = requestCount;
            _coolDownTime = coolDownTime;
            _watchdog = watchdog ?? throw new ArgumentNullException(nameof(watchdog));
            _metrics = metrics;
            _batchSize = batchSize;
            _maximizeThroughput = maximizeThroughput;
            _httpIteration = httpIteration ?? throw new ArgumentNullException();
            _iterationStatusMonitor = iterationStatusMonitor ?? throw new ArgumentNullException();

        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            var awaitableTasks = new BatchTaskTracker();
            Exception executionError = null;
            var coolDownWatch = Stopwatch.StartNew();

            bool continueCondition() => _requestCount > 0 && !cancellationToken.IsCancellationRequested;
            Func<bool> batchCondition = () => !cancellationToken.IsCancellationRequested;
            bool newBatch = true;

            IDisposable cooling = null;
            try
            {
                while (continueCondition() && !await _iterationStatusMonitor.IsTerminatedAsync(_httpIteration, cancellationToken))
                {
                    int batchSize = Math.Min(_batchSize, _requestCount);
                    if (_maximizeThroughput)
                    {
                        if (newBatch)
                        {
                            cooling?.Dispose();
                            cooling = null;
                            coolDownWatch.Restart();
                            await Task.Yield();
                            await _watchdog.BalanceAsync(_httpIteration.HttpRequest.Url.HostName, cancellationToken);
                            awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, batchSize, batchCondition, cancellationToken));
                            _requestCount -= batchSize;
                            if (continueCondition() && coolDownWatch.Elapsed.TotalMilliseconds < _coolDownTime)
                                cooling = _metrics?.BeginBatchCooldown(_httpIteration);
                        }
                        newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= _coolDownTime;
                    }
                    else
                    {
                        coolDownWatch.Restart();
                        await _watchdog.BalanceAsync(_httpIteration.HttpRequest.Url.HostName, cancellationToken);
                        awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, batchSize, batchCondition, cancellationToken));
                        _requestCount -= batchSize;
                        if (continueCondition())
                        {
                            var delay = Math.Max(_coolDownTime, _coolDownTime - (int)coolDownWatch.ElapsedMilliseconds);
                            using var pause = _metrics?.BeginBatchCooldown(_httpIteration);
                            await Task.Delay(delay, cancellationToken);
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

            return await awaitableTasks.CompleteAsync(executionError);
        }
    }
}
