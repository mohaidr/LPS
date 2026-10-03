using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Grpc.Core;
using LPS.Infrastructure.Distributed;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using NodeStatus = LPS.Infrastructure.Nodes.NodeStatus;
using NodeMetadata = LPS.Infrastructure.Nodes.NodeMetadata;

namespace LPS.UI.Core.Distributed;

internal sealed class MasterCoordinator(ClusterRunSettings settings, INodeRegistry nodes, IClusterConfiguration cluster) : IWorkerCoordinator
{
    public const int ProtocolVersion = 1;
    public const string LocalWorkerName = "master-local";
    public static string Version => typeof(MasterCoordinator).Assembly.GetName().Version!.ToString();
    private readonly object _admission = new();
    private readonly ConcurrentDictionary<string, WorkerSession> _sessions = new(StringComparer.Ordinal);
    private readonly Channel<WorkerSession> _joined = Channel.CreateBounded<WorkerSession>(settings.ExpectedWorkers);
    private bool _sealed;

    public async Task ConnectAsync(IAsyncStreamReader<WorkerUpdate> updates, IServerStreamWriter<WorkerInstruction> instructions, CancellationToken token)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        lifetime.CancelAfter(TimeSpan.FromSeconds(10));
        if (!await updates.MoveNext(lifetime.Token)) return;
        var hello = updates.Current;
        if (!Regex.IsMatch(hello.WorkerId, "^[A-Za-z0-9_-]{1,64}$") || !Guid.TryParse(hello.SessionId, out _)
            || hello.State != WorkerRunState.Idle || hello.ProtocolVersion != ProtocolVersion || hello.LpsVersion != Version)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Invalid worker identity, state, or incompatible LPS/protocol version."));
        var endpoint = ClusterRunSettings.ValidateEndpoint(hello.Endpoint).GetLeftPart(UriPartial.Authority);
        if (new Uri(endpoint).Port == 0 || endpoint == settings.MasterAddress)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Worker endpoint must have its own nonzero port."));
        var session = new WorkerSession(hello.WorkerId, hello.SessionId, settings.RunId, endpoint);
        lock (_admission)
        {
            if (_sessions.Values.Any(existing => existing.Endpoint == endpoint))
                throw new RpcException(new Status(StatusCode.AlreadyExists, "Worker endpoint is already assigned."));
            if (settings.MasterNodeIsWorker && session.WorkerId != LocalWorkerName
                && _sessions.Values.Count(existing => existing.WorkerId != LocalWorkerName) >= settings.ExpectedWorkers - 1)
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "The remaining worker slot is reserved for the master."));
            if (_sealed || _sessions.Count >= settings.ExpectedWorkers)
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "This run already has its worker set."));
            if (!_sessions.TryAdd(session.WorkerId, session))
                throw new RpcException(new Status(StatusCode.AlreadyExists, "Worker identity is already connected."));
            if (!_joined.Writer.TryWrite(session))
            {
                _sessions.TryRemove(session.WorkerId, out _);
                throw new RpcException(new Status(StatusCode.Unavailable, "Master admission queue is full. Retry registration."));
            }
        }
        lifetime.CancelAfter(Timeout.InfiniteTimeSpan);
        var reader = ReadUpdatesAsync(session, updates, lifetime.Token);
        var writer = WriteCommandsAsync(session, instructions, lifetime.Token);
        var heartbeat = KeepAliveAsync(session, lifetime.Token);
        try
        {
            await await Task.WhenAny(reader, writer, heartbeat);
        }
        finally
        {
            lifetime.Cancel();
            session.Commands.Writer.TryComplete();
            session.Disconnected();
            if (!string.IsNullOrEmpty(session.Endpoint))
                foreach (var node in nodes.Query(node => node.Metadata.NodeIP == session.Endpoint))
                    await node.SetNodeStatus(session.State == WorkerRunState.Completed ? NodeStatus.Stopped : NodeStatus.Failed);
            lock (_admission)
                if (!_sealed) _sessions.TryRemove(new KeyValuePair<string, WorkerSession>(session.WorkerId, session));
            try { await Task.WhenAll(reader, writer, heartbeat); }
            catch (OperationCanceledException) { }
            catch (RpcException) { }
            catch (InvalidOperationException) { }
        }
    }

    public async Task<bool> RunAsync(string planJson, CancellationToken token)
    {
        if (Encoding.UTF8.GetByteCount(planJson) > 1024 * 1024)
            throw new InvalidOperationException("Distributed plans are limited to 1 MiB. Provision large data files locally.");
        var selected = new List<WorkerSession>();
        using var preparation = CancellationTokenSource.CreateLinkedTokenSource(token);
        preparation.CancelAfter(TimeSpan.FromSeconds(settings.RegistrationTimeoutSeconds));
        try
        {
            while (selected.Count < settings.ExpectedWorkers)
            {
                var session = await _joined.Reader.ReadAsync(preparation.Token);
                if (session.Connected) selected.Add(session);
            }
            lock (_admission) _sealed = true;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planJson)));
            foreach (var session in selected)
            {
                session.Assign();
                var metadata = new NodeMetadata(cluster, session.WorkerId, session.Endpoint, "", "", "", "", 0, "", [], []);
                var node = new RunNode(metadata);
                await node.SetNodeStatus(NodeStatus.Stopped);
                nodes.RegisterNode(node);
                await session.Commands.Writer.WriteAsync(new WorkerInstruction
                {
                    RunId = settings.RunId, SessionId = session.SessionId, Action = WorkerAction.Prepare,
                    PlanJson = planJson, PlanHash = hash, MasterEndpoint = settings.MasterAddress,
                    RunToken = settings.Token, LeaseSeconds = settings.LeaseSeconds, StartedUtc = settings.StartedUtc.ToString("O")
                }, preparation.Token);
            }
            if (!await WaitForStatesAsync(selected.Select(session => session.Ready.Task), WorkerRunState.Ready, preparation.Token)) return false;
            foreach (var session in selected)
            {
                session.AuthorizeStart();
                await session.Commands.Writer.WriteAsync(Command(session, WorkerAction.Start), token);
            }
            return await WaitForStatesAsync(selected.Select(session => session.Finished.Task), WorkerRunState.Completed, token);
        }
        finally
        {
            var assigned = selected.Where(session => session.State != WorkerRunState.Idle).ToArray();
            foreach (var session in assigned.Where(session => !session.Finished.Task.IsCompleted))
                session.Commands.Writer.TryWrite(Command(session, WorkerAction.Cancel));
            try { await Task.WhenAll(assigned.Select(session => session.Finished.Task)).WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (TimeoutException) { }
            await File.WriteAllTextAsync(Path.Combine(settings.RunDirectory, "cluster-result.json"), JsonSerializer.Serialize(new
            {
                settings.RunId,
                Workers = selected.Select(session => new { session.WorkerId, session.SessionId, session.Endpoint, State = session.State.ToString() })
            }));
        }
    }

    public async Task ReleaseAsync()
    {
        var sessions = _sessions.Values.ToArray();
        var assigned = sessions.Where(session => session.State != WorkerRunState.Idle).ToArray();
        foreach (var session in assigned)
            session.Commands.Writer.TryWrite(Command(session, WorkerAction.Release));
        try { await Task.WhenAll(assigned.Where(session => session.Connected).Select(session => session.Released.Task)).WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TimeoutException) { }
        foreach (var session in sessions) session.Commands.Writer.TryComplete();
    }

    private static async Task<bool> WaitForStatesAsync(IEnumerable<Task<WorkerRunState>> tasks, WorkerRunState expected, CancellationToken token)
    {
        var pending = tasks.ToList();
        while (pending.Count > 0)
        {
            var finished = await Task.WhenAny(pending).WaitAsync(token);
            if (await finished != expected) return false;
            pending.Remove(finished);
        }
        return true;
    }

    private WorkerInstruction Command(WorkerSession session, WorkerAction action) => new()
    {
        RunId = settings.RunId, SessionId = session.SessionId, Action = action, LeaseSeconds = settings.LeaseSeconds
    };

    private async Task ReadUpdatesAsync(WorkerSession session, IAsyncStreamReader<WorkerUpdate> updates, CancellationToken token)
    {
        while (await updates.MoveNext(token))
        {
            var update = updates.Current;
            if (update.SessionId != session.SessionId || update.WorkerId != session.WorkerId)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Worker session identity mismatch."));
            session.Touch();
            if (update.Released)
            {
                if (update.RunId != settings.RunId || !session.Finished.Task.IsCompleted)
                    throw new InvalidOperationException("Unexpected run release acknowledgement.");
                session.Released.TrySetResult();
                continue;
            }
            if (update.State == WorkerRunState.Unknown) continue;
            if (update.State == WorkerRunState.Ready)
            {
                ClusterRunSettings.ValidateEndpoint(update.Endpoint);
                if (update.Endpoint == settings.MasterAddress) throw new InvalidOperationException("Worker and master endpoints must differ.");
            }
            if (!session.Apply(update)) continue;
            foreach (var node in nodes.Query(node => node.Metadata.NodeIP == session.Endpoint))
                await node.SetNodeStatus(update.State switch
                {
                    WorkerRunState.Ready => NodeStatus.Ready,
                    WorkerRunState.Completed or WorkerRunState.Cancelled or WorkerRunState.Failed =>
                        session.Ready.Task.IsCompletedSuccessfully && session.Ready.Task.Result == WorkerRunState.Ready ? NodeStatus.Running : NodeStatus.Failed,
                    _ => NodeStatus.Running
                });
            if (update.State == WorkerRunState.Ready) session.PublishReady();
        }
    }

    private static async Task WriteCommandsAsync(WorkerSession session, IServerStreamWriter<WorkerInstruction> instructions, CancellationToken token)
    {
        await foreach (var command in session.Commands.Reader.ReadAllAsync(token))
            await instructions.WriteAsync(command, token);
    }

    private async Task KeepAliveAsync(WorkerSession session, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(token))
        {
            if (session.Expired(TimeSpan.FromSeconds(settings.LeaseSeconds)))
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Worker heartbeat expired."));
            await session.Commands.Writer.WriteAsync(Command(session, WorkerAction.KeepAlive), token);
        }
    }
}