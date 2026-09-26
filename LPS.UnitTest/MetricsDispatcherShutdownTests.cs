using System.Threading.Channels;
using Apis.Hubs;
using Apis.Services;
using LPS.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Hosts;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.Infrastructure.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace LPS.UnitTest
{
    public class MetricsDispatcherShutdownTests
    {
        [Theory]
        [InlineData(typeof(CumulativeMetricsDispatcher), "ReceiveCumulativeMetrics")]
        [InlineData(typeof(WindowedMetricsDispatcher), "ReceiveWindowedMetrics")]
        [InlineData(typeof(HostCumulativeMetricsDispatcher), "ReceiveCumulativeHostMetrics")]
        [InlineData(typeof(HostWindowedMetricsDispatcher), "ReceiveWindowedHostMetrics")]
        public async Task RegisteredDispatcher_WaitsForFinalSignalRSend(Type dispatcherType, string eventName)
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = new Mock<IClientProxy>();
            client.Setup(proxy => proxy.SendCoreAsync(eventName, It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    entered.TrySetResult();
                    return release.Task;
                });
            var hub = new Mock<IHubContext<MetricsHub>>();
            hub.Setup(context => context.Clients.Group(It.IsAny<string>())).Returns(client.Object);
            var services = new ServiceCollection();
            services.AddLogging();
            new LPS.Apis.Startup().ConfigureServices(services);
            services.AddSingleton(hub.Object);
            services.AddSingleton(Mock.Of<IInfluxDBWriter>());
            services.AddSingleton(Mock.Of<INodeMetadata>(node => node.NodeType == NodeType.Master));
            services.AddSingleton<ICumulativeMetricsQueue, CumulativeMetricsQueue>();
            services.AddSingleton<IWindowedMetricsQueue, WindowedMetricsQueue>();
            services.AddSingleton<IHostCumulativeMetricsQueue, HostCumulativeMetricsQueue>();
            services.AddSingleton<IHostWindowedMetricsQueue, HostWindowedMetricsQueue>();
            using var provider = services.BuildServiceProvider();
            var dispatchers = provider.GetServices<IMetricsDispatcher>().ToArray();
            Assert.Equal(4, dispatchers.Length);
            var dispatcher = Assert.Single(dispatchers, current => current.GetType() == dispatcherType);
            Assert.Same(dispatcher, Assert.Single(provider.GetServices<IHostedService>(), current => current.GetType() == dispatcherType));
            var hosted = (IHostedService)dispatcher;
            await hosted.StartAsync(CancellationToken.None);
            try
            {
                switch (dispatcher)
                {
                    case CumulativeMetricsDispatcher:
                        provider.GetRequiredService<ICumulativeMetricsQueue>().TryEnqueue(new CumulativeIterationSnapshot { IsFinal = true, ExecutionStatus = "Success" });
                        break;
                    case WindowedMetricsDispatcher:
                        provider.GetRequiredService<IWindowedMetricsQueue>().TryEnqueue(new WindowedIterationSnapshot { IsFinal = true, ExecutionStatus = "Success" });
                        break;
                    case HostCumulativeMetricsDispatcher:
                        provider.GetRequiredService<IHostCumulativeMetricsQueue>().TryEnqueue(new HostCumulativeMetricsSnapshot { IsFinal = true, ExecutionStatus = "Completed" });
                        break;
                    case HostWindowedMetricsDispatcher:
                        provider.GetRequiredService<IHostWindowedMetricsQueue>().TryEnqueue(new HostWindowedMetricsSnapshot { IsFinal = true, ExecutionStatus = "Completed" });
                        break;
                }
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
                var completion = dispatcher.CompleteAsync(CancellationToken.None);
                Assert.False(completion.IsCompleted);

                release.TrySetResult();
                await completion.WaitAsync(TimeSpan.FromSeconds(3));
                client.Verify(proxy => proxy.SendCoreAsync(eventName, It.IsAny<object[]>(), CancellationToken.None), Times.AtLeastOnce);
            }
            finally
            {
                release.TrySetResult();
                await hosted.StopAsync(CancellationToken.None);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Completion_WaitsForInFlightSendEvenWhenQueueIsEmpty(bool stopService)
        {
            var queue = Channel.CreateUnbounded<int>();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var delivered = new List<int>();
            using var dispatcher = new TestDispatcher(queue, async snapshot =>
            {
                if (snapshot == 1)
                {
                    entered.TrySetResult();
                    await release.Task;
                }
                delivered.Add(snapshot);
            });
            await dispatcher.StartAsync(CancellationToken.None);
            queue.Writer.TryWrite(1);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, queue.Reader.Count);

            var complete = stopService ? dispatcher.StopAsync(CancellationToken.None) : dispatcher.CompleteAsync(CancellationToken.None);
            Assert.False(complete.IsCompleted);
            Assert.Empty(delivered);
            Assert.False(queue.Writer.TryWrite(2));

            release.TrySetResult();
            await complete.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(new[] { 1 }, delivered);
        }

        [Fact]
        public async Task Completion_DrainsBacklogInOrderWithoutAnotherReader()
        {
            var queue = Channel.CreateUnbounded<int>();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var delivered = new List<int>();
            using var dispatcher = new TestDispatcher(queue, async snapshot =>
            {
                if (snapshot == 1)
                {
                    entered.TrySetResult();
                    await release.Task;
                }
                delivered.Add(snapshot);
            });
            await dispatcher.StartAsync(CancellationToken.None);
            queue.Writer.TryWrite(1);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            queue.Writer.TryWrite(2);
            queue.Writer.TryWrite(3);

            var complete = dispatcher.CompleteAsync(CancellationToken.None);
            var stop = dispatcher.StopAsync(CancellationToken.None);
            Assert.False(complete.IsCompleted);
            Assert.False(stop.IsCompleted);
            Assert.Empty(delivered);

            release.TrySetResult();
            await Task.WhenAll(complete, stop).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(new[] { 1, 2, 3 }, delivered);
        }

        [Fact]
        public async Task Completion_BeforeStartupStillWaitsForDelivery()
        {
            var queue = Channel.CreateUnbounded<int>();
            var delivered = new List<int>();
            using var dispatcher = new TestDispatcher(queue, snapshot =>
            {
                delivered.Add(snapshot);
                return Task.CompletedTask;
            });
            queue.Writer.TryWrite(1);

            var complete = dispatcher.CompleteAsync(CancellationToken.None);
            Assert.False(complete.IsCompleted);
            await dispatcher.StartAsync(CancellationToken.None);
            await complete.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.Equal(new[] { 1 }, delivered);
        }

        private sealed class TestDispatcher(Channel<int> queue, Func<int, Task> send)
            : MetricsDispatcher<int>(queue.Reader, () => queue.Writer.TryComplete())
        {
            protected override Task PushSnapshotAsync(int snapshot, CancellationToken token) => send(snapshot);
        }
    }
}