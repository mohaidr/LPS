using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal class CBMode : IIterationModeService
    {
        private HttpRequestProfile.ExecuteCommand _command;
        private int _coolDownTime;
        private int _batchSize;
        private bool _maximizeThroughput;
        private IBatchProcessor<HttpRequestProfile.ExecuteCommand, HttpRequestProfile> _batchProcessor;

        private CBMode() { }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            int numberOfSentRequests = 0;
            var coolDownWatch = Stopwatch.StartNew();

            Func<bool> continueCondition = () => !cancellationToken.IsCancellationRequested;
            Func<bool> batchCondition = continueCondition;

            while (continueCondition())
            {
                if (_maximizeThroughput)
                {
                    bool newBatch = coolDownWatch.Elapsed.TotalMilliseconds >= _coolDownTime;
                    if (newBatch)
                    {
                        coolDownWatch.Restart();
                        await Task.Yield();
                        numberOfSentRequests+= await _batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition);
                    }
                }
                else
                {
                    coolDownWatch.Restart();
                    numberOfSentRequests += await _batchProcessor.SendBatchAsync(_command, _batchSize, batchCondition);
                    await Task.Delay((int)Math.Max(_coolDownTime, _coolDownTime - coolDownWatch.ElapsedMilliseconds), cancellationToken);
                }
            }

            coolDownWatch.Stop();
            return numberOfSentRequests;
        }

        public class Builder : IBuilder<CBMode>
        {
            private HttpRequestProfile.ExecuteCommand _command;
            private int _coolDownTime;
            private int _batchSize;
            private bool _maximizeThroughput;
            private IBatchProcessor<HttpRequestProfile.ExecuteCommand, HttpRequestProfile> _batchProcessor;

            public Builder SetCommand(HttpRequestProfile.ExecuteCommand command)
            {
                _command = command;
                return this;
            }

            public Builder SetCoolDownTime(int coolDownTime)
            {
                _coolDownTime = coolDownTime;
                return this;
            }

            public Builder SetBatchSize(int batchSize)
            {
                _batchSize = batchSize;
                return this;
            }

            public Builder SetMaximizeThroughput(bool maximizeThroughput)
            {
                _maximizeThroughput = maximizeThroughput;
                return this;
            }


            public Builder SetBatchProcessor(IBatchProcessor<HttpRequestProfile.ExecuteCommand, HttpRequestProfile> batchProcessor)
            {
                _batchProcessor = batchProcessor;
                return this;
            }

            public CBMode Build()
            {
                var cbMode = new CBMode
                {
                    _command = _command,
                    _coolDownTime = _coolDownTime,
                    _batchSize = _batchSize,
                    _maximizeThroughput = _maximizeThroughput,
                    _batchProcessor = _batchProcessor
                };
                return cbMode;
            }
        }
    }
}
