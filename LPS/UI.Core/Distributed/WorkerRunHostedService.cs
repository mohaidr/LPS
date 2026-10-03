using System.Text.Json;
using LPS.Infrastructure.Distributed;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.UI.Common;
using LPS.UI.Core.LPSCommandLine;
using Microsoft.Extensions.Hosting;

namespace LPS.UI.Core.Distributed;

internal sealed class WorkerRunHostedService(
    ClusterRunSettings settings,
    ITestExecutionService execution,
    ICumulativeMetricsCoordinator cumulative,
    IWindowedMetricsCoordinator windowed,
    DistributedRunFinalizer finalizer,
    IHostApplicationLifetime lifetime,
    CancellationTokenSource cancellation) : BackgroundService
{
    private readonly TaskCompletionSource _start = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var stopping = stoppingToken.Register(cancellation.Cancel);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);
        var input = Task.Run(() => ReadCommandsAsync(stoppingToken), CancellationToken.None);
        var state = "Failed";
        try
        {
            var parameters = new TestRunParameters(settings.PlanPath, [], [], [], cancellation.Token);
            if (!await execution.PrepareAsync(parameters)) throw new InvalidOperationException("Worker plan preparation failed.");
            await File.WriteAllTextAsync(Path.Combine(settings.RunDirectory, "ready"), settings.RunId, cancellation.Token);
            await _start.Task.WaitAsync(cancellation.Token);
            await cumulative.StartAsync(cancellation.Token);
            await windowed.StartAsync(cancellation.Token);
            await execution.ExecutePreparedAsync(parameters);
            state = cancellation.IsCancellationRequested ? "Cancelled" : "Completed";
        }
        catch (OperationCanceledException) { state = "Cancelled"; }
        catch (Exception exception) { Console.Error.WriteLine($"Worker execution failed: {exception.GetType().Name}."); }
        finally
        {
            try { await finalizer.CompleteAsync(); }
            catch (Exception exception) { state = "Failed"; Console.Error.WriteLine($"Worker finalization failed: {exception.GetType().Name}."); }
            var resultPath = Path.Combine(settings.RunDirectory, "completion.json");
            await File.WriteAllTextAsync(resultPath + ".tmp", JsonSerializer.Serialize(new { settings.RunId, State = state }));
            File.Move(resultPath + ".tmp", resultPath, true);
            Environment.ExitCode = state == "Completed" ? 0 : 1;
            try { await _release.Task.WaitAsync(TimeSpan.FromSeconds(40), stoppingToken); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
            lifetime.StopApplication();
            try { await input.WaitAsync(stoppingToken); } catch (OperationCanceledException) { }
        }
    }

    private async Task ReadCommandsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var command = await Console.In.ReadLineAsync(token);
            if (command is null) { cancellation.Cancel(); _release.TrySetResult(); return; }
            if (command == "stop") cancellation.Cancel();
            if (command == "start") _start.TrySetResult();
            if (command == "release") { _release.TrySetResult(); return; }
        }
    }
}