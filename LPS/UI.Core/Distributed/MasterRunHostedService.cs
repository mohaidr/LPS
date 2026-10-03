using LPS.Infrastructure.Distributed;
using LPS.Infrastructure.Nodes;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.UI.Common;
using LPS.UI.Core.LPSCommandLine;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Nodes;
using LPS.UI.Core.Host;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Options;

namespace LPS.UI.Core.Distributed;

internal sealed class MasterRunHostedService(
    ClusterRunSettings settings,
    ITestExecutionService execution,
    MasterCoordinator coordinator,
    INodeRegistry nodes,
    ICumulativeMetricsCoordinator cumulative,
    IWindowedMetricsCoordinator windowed,
    DistributedRunFinalizer finalizer,
    IDashboardService dashboard,
    IOptions<DashboardConfigurationOptions> dashboardOptions,
    IHostApplicationLifetime lifetime,
    CancellationTokenSource cancellation) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var stopping = stoppingToken.Register(cancellation.Cancel);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);
        var input = Task.Run(() => WatchInputAsync(stoppingToken), CancellationToken.None);
        using var localWorkerCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var localWorker = Task.CompletedTask;
        var prepared = false;
        try
        {
            var parameters = new TestRunParameters(settings.PlanPath, [], [], [], cancellation.Token);
            if (!await execution.PrepareAsync(parameters)) throw new InvalidOperationException("Master plan preparation failed.");
            prepared = true;
            await nodes.GetLocalNode().SetNodeStatus(NodeStatus.Ready);
            await cumulative.StartAsync(cancellation.Token);
            await windowed.StartAsync(cancellation.Token);
            dashboard.Start();
            await File.WriteAllTextAsync(Path.Combine(settings.RunDirectory, "ready"), settings.RunId, cancellation.Token);
            Console.WriteLine($"Master ready: {settings.NodeAddress}; run {settings.RunId}; waiting for {settings.ExpectedWorkers} worker(s).");
            if (settings.MasterNodeIsWorker)
            {
                Console.WriteLine("Master load execution: local worker master-local.");
                localWorker = new WorkerAgent(new WorkerAgentOptions
                {
                    Name = MasterCoordinator.LocalWorkerName, MasterAddress = settings.MasterAddress,
                    ListenAddress = new UriBuilder(settings.NodeAddress) { Port = 0 }.Uri.GetLeftPart(UriPartial.Authority),
                    DataDirectory = Path.Combine(settings.RunDirectory, "local-worker"),
                    SettingsPath = Path.Combine(settings.RunDirectory, "settings.json"), Token = settings.RegistrationToken!,
                    CertificatePath = settings.CertificatePath, CertificatePassword = settings.CertificatePassword,
                    CertificateAuthorityPath = settings.CertificateAuthorityPath
                }).RunAsync(localWorkerCancellation.Token);
            }
            var successful = await coordinator.RunAsync(await File.ReadAllTextAsync(settings.PlanPath, cancellation.Token), cancellation.Token);
            Environment.ExitCode = successful ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Master run cancelled or worker preparation timed out.");
            Environment.ExitCode = 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Master run failed: {exception.GetType().Name}.");
            Environment.ExitCode = 1;
        }
        finally
        {
            try
            {
                try { await finalizer.CompleteAsync(); }
                catch (Exception exception) { Console.Error.WriteLine($"Master finalization failed: {exception.GetType().Name}."); Environment.ExitCode = 1; }
                if (execution.HasFailedIterations) Environment.ExitCode = 1;
                var resultPath = Path.Combine(settings.RunDirectory, "cluster-result.json");
                var result = File.Exists(resultPath) ? JsonNode.Parse(await File.ReadAllTextAsync(resultPath))!.AsObject()
                    : new JsonObject { ["RunId"] = settings.RunId, ["Workers"] = new JsonArray() };
                result["State"] = Environment.ExitCode == 0 ? "Completed" : cancellation.IsCancellationRequested ? "Cancelled" : "Failed";
                await File.WriteAllTextAsync(resultPath, result.ToJsonString());
                Console.WriteLine($"Distributed run: {result["State"]}. Results: {resultPath}");
                if (prepared && dashboardOptions.Value.BuiltInDashboard == true)
                    await dashboard.EnsureDashboardUpdateBeforeExitAsync();
                await coordinator.ReleaseAsync();
            }
            finally
            {
                localWorkerCancellation.Cancel();
                try { await localWorker; }
                catch (OperationCanceledException) { }
                catch (Exception exception) { Console.Error.WriteLine($"Local worker shutdown failed: {exception.GetType().Name}."); Environment.ExitCode = 1; }
                await nodes.GetLocalNode().SetNodeStatus(NodeStatus.Stopped);
                lifetime.StopApplication();
                try { await input.WaitAsync(stoppingToken); } catch (OperationCanceledException) { }
            }
        }
    }

    private async Task WatchInputAsync(CancellationToken token)
    {
        if (!Console.IsInputRedirected) return;
        while (!token.IsCancellationRequested)
        {
            var command = await Console.In.ReadLineAsync(token);
            if (command is null or "stop") { cancellation.Cancel(); return; }
        }
    }
}