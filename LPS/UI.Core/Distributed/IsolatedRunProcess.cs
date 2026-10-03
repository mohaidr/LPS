using System.Diagnostics;
using System.Text.Json;
using LPS.Infrastructure.Distributed;
using LPS.Protos.Shared;

namespace LPS.UI.Core.Distributed;

internal sealed class IsolatedRunProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ClusterRunSettings _settings;
    private readonly SemaphoreSlim _input = new(1, 1);
    private readonly Task _output;
    private int _started;
    private bool _ready;
    public string RunId => _settings.RunId;
    public string Endpoint => _settings.NodeAddress;
    public Task Exited { get; }
    public int ExitCode => _process.ExitCode;
    public WorkerRunState State { get; set; } = WorkerRunState.Preparing;
    public TaskCompletionSource StartReported { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IsolatedRunProcess(ClusterRunSettings settings, string settingsPath, bool echo)
    {
        _settings = settings;
        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = settings.RunDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(IsolatedRunProcess).Assembly.Location);
        start.ArgumentList.Add("cluster-run");
        start.Environment[ClusterRunSettings.EnvironmentKey] = Path.Combine(settings.RunDirectory, "run-settings.json");
        start.Environment["LPS_SETTINGS_FILE"] = settingsPath;
        start.Environment.Remove("LPS_WORKSPACE_RUN");
        start.Environment.Remove("LPS_CLUSTER_TOKEN");
        start.Environment.Remove("LPS_CLUSTER_CERT_PASSWORD");
        start.Environment["NO_COLOR"] = "1";
        _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the run process.");
        File.WriteAllText(Path.Combine(settings.RunDirectory, "process.json"), JsonSerializer.Serialize(new { ProcessId = _process.Id, settings.RunId }));
        Exited = _process.WaitForExitAsync();
        _output = Task.WhenAll(CaptureAsync(_process.StandardOutput, "stdout.log", echo), CaptureAsync(_process.StandardError, "stderr.log", echo));
    }

    public async Task WaitReadyAsync(CancellationToken token)
    {
        await WaitForFileAsync("ready", token);
        _ready = true;
    }

    public async Task<bool> StartAsync(CancellationToken token)
    {
        if (!_ready) throw new InvalidOperationException("Run is not prepared.");
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0) return false;
        await SendAsync("start", token);
        return true;
    }

    public async Task<WorkerRunState> WaitForCompletionAsync(CancellationToken token)
    {
        await WaitForFileAsync("completion.json", token);
        using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_settings.RunDirectory, "completion.json"), token));
        if (result.RootElement.GetProperty("RunId").GetString() != RunId) throw new InvalidOperationException("Run result identity mismatch.");
        return Enum.Parse<WorkerRunState>(result.RootElement.GetProperty("State").GetString()!);
    }

    public async Task ReleaseAsync(CancellationToken token)
    {
        await SendAsync("release", token);
        await Exited.WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    public Task CancelAsync(CancellationToken token) => SendAsync("stop", token);

    public async Task StopAsync()
    {
        if (_process.HasExited) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await SendAsync("stop", timeout.Token);
            await SendAsync("release", timeout.Token);
            await Exited.WaitAsync(timeout.Token);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException)
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            await Exited.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private async Task SendAsync(string command, CancellationToken token)
    {
        await _input.WaitAsync(token);
        try
        {
            if (_process.HasExited) return;
            await _process.StandardInput.WriteLineAsync(command.AsMemory(), token);
            await _process.StandardInput.FlushAsync(token);
        }
        finally { _input.Release(); }
    }

    private async Task WaitForFileAsync(string name, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (!File.Exists(Path.Combine(_settings.RunDirectory, name)))
        {
            if (name == "ready" && File.Exists(Path.Combine(_settings.RunDirectory, "completion.json")))
                throw new InvalidOperationException("Run preparation failed; see its private logs.");
            if (_process.HasExited) throw new InvalidOperationException($"Run process exited before {name}; see its private logs.");
            await timer.WaitForNextTickAsync(token);
        }
    }

    private async Task CaptureAsync(StreamReader reader, string name, bool echo)
    {
        await using var writer = new StreamWriter(Path.Combine(_settings.RunDirectory, name));
        var written = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (echo) Console.WriteLine(line);
            if (written >= 2_000_000) continue;
            var text = line[..Math.Min(line.Length, 4000)];
            await writer.WriteLineAsync(text);
            written += text.Length;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _output;
        _process.Dispose();
        _input.Dispose();
    }
}