using System.Diagnostics;
using System.Threading.Channels;
using LPS.Protos.Shared;

namespace LPS.UI.Core.Distributed;

internal sealed class WorkerSession(string workerId, string sessionId, string runId, string endpoint = "")
{
    private readonly object _gate = new();
    private WorkerRunState _state = WorkerRunState.Idle;
    private bool _startAuthorized;
    private bool _connected = true;
    private long _lastSeen = Stopwatch.GetTimestamp();
    public string WorkerId { get; } = workerId;
    public string SessionId { get; } = sessionId;
    public string RunId { get; } = runId;
    public string Endpoint { get; private set; } = endpoint;
    public WorkerRunState State { get { lock (_gate) return _state; } }
    public Channel<WorkerInstruction> Commands { get; } = Channel.CreateBounded<WorkerInstruction>(16);
    public TaskCompletionSource<WorkerRunState> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<WorkerRunState> Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Connected => Volatile.Read(ref _connected);
    public void Touch() => Interlocked.Exchange(ref _lastSeen, Stopwatch.GetTimestamp());
    public bool Expired(TimeSpan lease) => Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastSeen)) > lease;

    public void Assign()
    {
        lock (_gate)
        {
            if (_state != WorkerRunState.Idle) throw new InvalidOperationException("Worker is not idle.");
            _state = WorkerRunState.Preparing;
        }
    }

    public void AuthorizeStart()
    {
        lock (_gate)
        {
            if (_state != WorkerRunState.Ready) throw new InvalidOperationException("Worker is not ready.");
            _startAuthorized = true;
        }
    }

    public bool Apply(WorkerUpdate update)
    {
        lock (_gate)
        {
            if (update.WorkerId != WorkerId || update.SessionId != SessionId || update.RunId != RunId)
                throw new InvalidOperationException("Stale or mismatched worker update.");
            if (update.State == WorkerRunState.Ready && Endpoint.Length > 0 && update.Endpoint != Endpoint)
                throw new InvalidOperationException("An assigned endpoint cannot change.");
            if (update.State == _state)
            {
                if (_state == WorkerRunState.Ready && update.Endpoint != Endpoint)
                    throw new InvalidOperationException("A prepared endpoint cannot change.");
                return false;
            }
            var allowed = update.State switch
            {
                WorkerRunState.Ready => _state == WorkerRunState.Preparing,
                WorkerRunState.Running => _state == WorkerRunState.Ready && _startAuthorized,
                WorkerRunState.Finalizing => _state == WorkerRunState.Running,
                WorkerRunState.Completed => _state == WorkerRunState.Finalizing,
                WorkerRunState.Failed or WorkerRunState.Cancelled => !Finished.Task.IsCompleted,
                _ => false
            };
            if (!allowed) throw new InvalidOperationException($"Invalid worker transition: {_state} -> {update.State}.");
            _state = update.State;
            if (_state == WorkerRunState.Ready) Endpoint = update.Endpoint;
            if (_state is WorkerRunState.Failed or WorkerRunState.Cancelled)
                Ready.TrySetResult(_state);
            if (_state is WorkerRunState.Completed or WorkerRunState.Failed or WorkerRunState.Cancelled)
                Finished.TrySetResult(_state);
            return true;
        }
    }

    public void PublishReady()
    {
        lock (_gate)
        {
            if (_state != WorkerRunState.Ready) throw new InvalidOperationException("Worker is not ready.");
            Ready.TrySetResult(_state);
        }
    }

    public void Disconnected()
    {
        lock (_gate)
        {
            Volatile.Write(ref _connected, false);
            if (Finished.Task.IsCompleted) return;
            _state = WorkerRunState.Failed;
            Ready.TrySetResult(_state);
            Finished.TrySetResult(_state);
        }
    }
}