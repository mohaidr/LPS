using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.UnitTest
{
    public class MetricsCoordinatorShutdownTests
    {
        [Fact]
        public async Task WindowedCoordinator_StopAsync_WaitsForFinalSnapshotHandler()
        {
            using var coordinator = new WindowedMetricsCoordinator(TimeSpan.FromHours(1));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.OnWindowClosed += async () =>
            {
                entered.SetResult();
                await release.Task;
            };
            coordinator.Start();

            var stopTask = coordinator.StopAsync(CancellationToken.None).AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(stopTask.IsCompleted);
            release.SetResult();
            await stopTask;
        }

        [Fact]
        public async Task CumulativeCoordinator_StopAsync_WaitsForFinalSnapshotHandler()
        {
            using var coordinator = new CumulativeMetricsCoordinator(TimeSpan.FromHours(1));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.OnPushInterval += async () =>
            {
                entered.SetResult();
                await release.Task;
            };
            coordinator.Start();

            var stopTask = coordinator.StopAsync(CancellationToken.None).AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(stopTask.IsCompleted);
            release.SetResult();
            await stopTask;
        }
    }
}
