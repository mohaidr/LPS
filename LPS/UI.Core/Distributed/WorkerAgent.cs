using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Grpc.Core;
using LPS.Infrastructure.Distributed;
using LPS.Protos.Shared;
using NodeType = LPS.Infrastructure.Nodes.NodeType;

namespace LPS.UI.Core.Distributed;

internal sealed class WorkerAgent(WorkerAgentOptions options)
{
    public async Task RunAsync(CancellationToken token)
    {
        Directory.CreateDirectory(options.DataDirectory);
        using var ownership = new FileStream(Path.Combine(options.DataDirectory, "worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var channel = ClusterGrpcTransport.CreateChannel(options.MasterAddress, token: options.Token, certificateAuthorityPath: options.CertificateAuthorityPath);
        var client = new WorkerControl.WorkerControlClient(channel);
        var delaySeconds = 1;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(client, token);
                delaySeconds = 1;
            }
            catch (RpcException exception) when (exception.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied or StatusCode.FailedPrecondition or StatusCode.Unimplemented)
            {
                Console.Error.WriteLine($"Worker registration rejected ({exception.StatusCode}). Check credentials and LPS versions.");
                Environment.ExitCode = 1;
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                Console.WriteLine($"Worker {options.Name}: waiting for master ({exception.GetType().Name}).");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(delaySeconds * 1000 + Random.Shared.Next(250)), token);
            delaySeconds = Math.Min(5, delaySeconds * 2);
        }
    }

    private async Task ConnectAsync(WorkerControl.WorkerControlClient client, CancellationToken token)
    {
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var call = client.Connect(cancellationToken: connection.Token);
        var sessionId = Guid.NewGuid().ToString();
        var endpoint = DistributedFiles.Endpoint(options.ListenAddress);
        var outgoing = Channel.CreateBounded<WorkerUpdate>(32);
        long lastCommand = Stopwatch.GetTimestamp();
        var leaseSeconds = 10;
        var registered = false;
        string? rejectedRun = null;
        IsolatedRunProcess? active = null;
        Task observation = Task.CompletedTask;

        WorkerUpdate Update(WorkerRunState state, string runId = "", string endpoint = "", string detail = "") => new()
        {
            WorkerId = options.Name, SessionId = sessionId, ProtocolVersion = MasterCoordinator.ProtocolVersion,
            LpsVersion = MasterCoordinator.Version, RunId = runId, State = state, Endpoint = endpoint, Detail = detail
        };

        async Task PublishAsync(IsolatedRunProcess process, WorkerRunState state)
        {
            process.State = state;
            await outgoing.Writer.WriteAsync(Update(state, process.RunId, process.Endpoint), connection.Token);
            Console.WriteLine($"Worker {options.Name}: {process.RunId} {state}.");
        }

        async Task ObserveAsync(IsolatedRunProcess process)
        {
            try
            {
                await process.WaitReadyAsync(connection.Token).WaitAsync(TimeSpan.FromSeconds(90), connection.Token);
                await PublishAsync(process, WorkerRunState.Ready);
                var result = await process.WaitForCompletionAsync(connection.Token);
                if (result == WorkerRunState.Completed)
                {
                    await process.StartReported.Task.WaitAsync(connection.Token);
                    await PublishAsync(process, WorkerRunState.Finalizing);
                }
                await PublishAsync(process, result);
            }
            catch (OperationCanceledException) when (connection.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Worker {options.Name}: preparation/execution failed ({exception.GetType().Name}); see private run logs.");
                await process.StopAsync();
                await PublishAsync(process, WorkerRunState.Failed);
            }
        }

        async Task WriteAsync()
        {
            await foreach (var update in outgoing.Reader.ReadAllAsync(connection.Token))
                await call.RequestStream.WriteAsync(update, connection.Token);
        }

        async Task HeartbeatAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(connection.Token))
                await outgoing.Writer.WriteAsync(Update(WorkerRunState.Unknown), connection.Token);
        }

        async Task WatchLeaseAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(connection.Token))
                if (Stopwatch.GetElapsedTime(Interlocked.Read(ref lastCommand)) > TimeSpan.FromSeconds(Volatile.Read(ref leaseSeconds)))
                {
                    Console.Error.WriteLine($"Worker {options.Name}: master lease expired; stopping the assigned run.");
                    connection.Cancel();
                    return;
                }
        }

        await outgoing.Writer.WriteAsync(Update(WorkerRunState.Idle, endpoint: endpoint), connection.Token);
        var writer = WriteAsync();
        var heartbeat = HeartbeatAsync();
        var watchdog = WatchLeaseAsync();
        try
        {
            while (await call.ResponseStream.MoveNext(connection.Token))
            {
                var command = call.ResponseStream.Current;
                if (command.SessionId != sessionId) throw new InvalidOperationException("Stale master session.");
                if (!registered)
                {
                    Console.WriteLine($"Worker {options.Name}: registered; waiting for assignment.");
                    registered = true;
                }
                Interlocked.Exchange(ref lastCommand, Stopwatch.GetTimestamp());
                Volatile.Write(ref leaseSeconds, Math.Clamp(command.LeaseSeconds, 3, 30));
                if (command.Action == WorkerAction.KeepAlive) continue;
                if (command.Action == WorkerAction.Prepare)
                {
                    if (active != null)
                    {
                        await outgoing.Writer.WriteAsync(active.RunId == command.RunId
                            ? Update(active.State, active.RunId, active.Endpoint)
                            : Update(WorkerRunState.Unknown, command.RunId, detail: "Worker is busy."), connection.Token);
                        continue;
                    }
                    try
                    {
                        active = await PrepareAsync(command, endpoint, connection.Token);
                        observation = ObserveAsync(active);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        rejectedRun = command.RunId;
                        Console.Error.WriteLine($"Worker {options.Name}: assignment rejected ({exception.GetType().Name}).");
                        await outgoing.Writer.WriteAsync(Update(WorkerRunState.Failed, command.RunId, detail: "Preparation rejected; check worker configuration and run history."), connection.Token);
                    }
                    continue;
                }
                if (active == null || active.RunId != command.RunId)
                {
                    var update = Update(WorkerRunState.Unknown, command.RunId, detail: "No matching active run.");
                    update.Released = active == null && rejectedRun == command.RunId && command.Action == WorkerAction.Release;
                    await outgoing.Writer.WriteAsync(update, connection.Token);
                    continue;
                }
                if (command.Action == WorkerAction.Start && await active.StartAsync(connection.Token))
                {
                    await PublishAsync(active, WorkerRunState.Running);
                    active.StartReported.TrySetResult();
                }
                else if (command.Action == WorkerAction.Cancel)
                    await active.CancelAsync(connection.Token);
                else if (command.Action == WorkerAction.Release && active.State is WorkerRunState.Completed or WorkerRunState.Failed or WorkerRunState.Cancelled)
                {
                    await active.ReleaseAsync(connection.Token);
                    var released = Update(WorkerRunState.Unknown, active.RunId);
                    released.Released = true;
                    await outgoing.Writer.WriteAsync(released, connection.Token);
                }
            }
        }
        finally
        {
            connection.Cancel();
            outgoing.Writer.TryComplete();
            if (active != null) await active.DisposeAsync();
            try { await Task.WhenAll(writer, heartbeat, watchdog, observation); }
            catch (OperationCanceledException) { }
            catch (RpcException) { }
        }
    }

    internal async Task<IsolatedRunProcess> PrepareAsync(WorkerInstruction command, string endpoint, CancellationToken token)
    {
        if (!Guid.TryParse(command.RunId, out var runId) || command.RunToken.Length < 32
            || Encoding.UTF8.GetByteCount(command.PlanJson) > 1024 * 1024
            || !string.Equals(ClusterRunSettings.ValidateEndpoint(command.MasterEndpoint).GetLeftPart(UriPartial.Authority), options.MasterAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command.PlanJson))), command.PlanHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid assignment identity, endpoint, size, or checksum.");
        using var plan = System.Text.Json.JsonDocument.Parse(command.PlanJson);
        if (plan.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new InvalidOperationException("A plan must be a JSON object.");
        var directory = Path.Combine(options.DataDirectory, "runs", runId.ToString("N"));
        Directory.CreateDirectory(directory);
        using (new FileStream(Path.Combine(directory, "claimed"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        var settings = new ClusterRunSettings
        {
            RunId = command.RunId, StartedUtc = DateTime.Parse(command.StartedUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
            NodeName = options.Name, RunDirectory = directory, PlanPath = Path.Combine(directory, "plan.json"),
            NodeAddress = endpoint, MasterAddress = options.MasterAddress,
            Role = NodeType.Worker, Token = command.RunToken, CertificatePath = options.CertificatePath,
            CertificatePassword = options.CertificatePassword, CertificateAuthorityPath = options.CertificateAuthorityPath
        };
        await DistributedFiles.WritePrivateAsync(settings.PlanPath, command.PlanJson, token);
        var settingsPath = await DistributedFiles.WriteSettingsAsync(settings, options.SettingsPath, 0, token);
        return new IsolatedRunProcess(settings, settingsPath, false);
    }
}