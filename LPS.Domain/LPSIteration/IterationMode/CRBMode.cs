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
    internal class CRBMode : IIterationModeService
    {
        private int _requestCount;
        private readonly HttpRequest.ExecuteCommand _command;
        private readonly int _coolDownTime;
        private readonly int _batchSize;
        private readonly bool _maximizeThroughput;
        private readonly IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> _batchProcessor;
        readonly HttpIteration _httpIteration;
        readonly ITerminationCheckerService _terminationCheckerService;

        public CRBMode(
            HttpRequest.ExecuteCommand command,
            int requestCount,
            int coolDownTime,
            int batchSize,
            bool maximizeThroughput,
            IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> batchProcessor,
            HttpIteration httpIteration,
            ITerminationCheckerService terminationCheckerService)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
            _requestCount = requestCount;
            _coolDownTime = coolDownTime;
            _batchSize = batchSize;
            _maximizeThroughput = maximizeThroughput;
            _httpIteration = httpIteration ?? throw new ArgumentNullException();
            _terminationCheckerService = terminationCheckerService ?? throw new ArgumentNullException();

        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            List<Task<int>> awaitableTasks = [];
            var coolDownWatch = Stopwatch.StartNew();

            bool continueCondition() => _requestCount > 0 && !cancellationToken.IsCancellationRequested;
            Func<bool> batchCondition = () => !cancellationToken.IsCancellationRequested;
            bool newBatch = true;

            while (continueCondition() && !await _terminationCheckerService.IsTerminationRequiredAsync(_httpIteration))
            {
                int batchSize = Math.Min(_batchSize, _requestCount);

                if (_maximizeThroughput)
                {
                    if (newBatch)
                    {
                        coolDownWatch.Restart();
                        await Task.Yield();
                        awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, batchSize, batchCondition, cancellationToken));
                        _requestCount -= batchSize;
                    }
                    newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= _coolDownTime;
                }
                else
                {
                    coolDownWatch.Restart();
                    awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, batchSize, batchCondition, cancellationToken));
                    _requestCount -= batchSize;

                    if (continueCondition())
                    {
                        var delay = Math.Max(_coolDownTime, _coolDownTime - (int)coolDownWatch.ElapsedMilliseconds);
                        await Task.Delay(delay, cancellationToken);
                    }
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
