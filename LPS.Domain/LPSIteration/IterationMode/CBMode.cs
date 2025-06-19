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
        readonly ITerminationCheckerService _terminationCheckerService;
        public CBMode(
            HttpRequest.ExecuteCommand command,
            int coolDownTime,
            int batchSize,
            bool maximizeThroughput,
            IBatchProcessor<HttpRequest.ExecuteCommand, HttpRequest> batchProcessor, 
            HttpIteration httpIteration,
            ITerminationCheckerService terminationCheckerService)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _coolDownTime = coolDownTime;
            _batchSize = batchSize;
            _maximizeThroughput = maximizeThroughput;
            _batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
            _httpIteration = httpIteration ?? throw new ArgumentNullException();
            _terminationCheckerService = terminationCheckerService ?? throw new ArgumentNullException();
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            var coolDownWatch = Stopwatch.StartNew();
            List<Task<int>> awaitableTasks = new();

            bool continueCondition() => !cancellationToken.IsCancellationRequested;
            Func<bool> batchCondition = continueCondition;
            bool newBatch = true;

            while (continueCondition() && !await _terminationCheckerService.Check(_httpIteration))
            {
                if (_maximizeThroughput)
                {
                    if (newBatch)
                    {
                        coolDownWatch.Restart();
                        await Task.Yield();
                        awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition, cancellationToken));
                    }
                    newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= _coolDownTime;
                }
                else
                {
                    coolDownWatch.Restart();
                    awaitableTasks.Add(_batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition, cancellationToken));
                    await Task.Delay((int)Math.Max(_coolDownTime, _coolDownTime - coolDownWatch.ElapsedMilliseconds), cancellationToken);
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
