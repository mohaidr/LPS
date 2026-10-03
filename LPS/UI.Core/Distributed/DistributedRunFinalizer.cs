using LPS.Common.Interfaces;
using LPS.Infrastructure.Monitoring;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.UI.Common;

namespace LPS.UI.Core.Distributed;

internal sealed class DistributedRunFinalizer(
    CoolingMetricsReporter cooling,
    ICumulativeMetricsCoordinator cumulative,
    IWindowedMetricsCoordinator windowed,
    IEnumerable<IMetricsDispatcher> dispatchers,
    ITestExecutionService execution)
{
    public async Task CompleteAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var token = timeout.Token;
        await cooling.CompleteAsync().WaitAsync(token);
        await cumulative.StopAsync(token).AsTask().WaitAsync(token);
        await windowed.StopAsync(token).AsTask().WaitAsync(token);
        await Task.WhenAll(dispatchers.Select(dispatcher => dispatcher.CompleteAsync(token))).WaitAsync(token);
        await execution.PersistMetricsAsync(token).WaitAsync(token);
    }
}