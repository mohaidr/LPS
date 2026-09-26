using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LPS.Common.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Apis.Services
{
    public abstract class MetricsDispatcher<TSnapshot> : BackgroundService, IMetricsDispatcher
    {
        private readonly ChannelReader<TSnapshot> _reader;
        private readonly Action _completeQueue;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected MetricsDispatcher(ChannelReader<TSnapshot> reader, Action completeQueue)
        {
            _reader = reader;
            _completeQueue = completeQueue;
        }

        public Task CompleteAsync(CancellationToken token)
        {
            _completeQueue();
            return _completion.Task.WaitAsync(token);
        }

        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var snapshot in _reader.ReadAllAsync())
                    await PushSnapshotAsync(snapshot, CancellationToken.None);

                _completion.TrySetResult();
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await CompleteAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }

        protected abstract Task PushSnapshotAsync(TSnapshot snapshot, CancellationToken token);
    }
}