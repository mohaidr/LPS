using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using LPS.Infrastructure.Common;
using LPS.UI.Common.DTOs;
using LPS.UI.Core.Web.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LPS.UI.Core.Web.Services;

public sealed class WorkspaceRunService : IWorkspaceRunService, IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IWorkspacePlanService _plans;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<WorkspaceRunService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory;
    private Process? _process;
    private WorkspaceRun? _activeRun;
    private Task _completion = Task.CompletedTask;
    private bool _stopRequested;

    public WorkspaceRunService(IWorkspacePlanService plans, WorkspaceOptions options, ILogger<WorkspaceRunService> logger)
    {
        _plans = plans;
        _options = options;
        _logger = logger;
        _directory = Path.Combine(Path.GetFullPath(options.DataDirectory), "runs");
        Directory.CreateDirectory(_directory);
    }

    public async Task<IReadOnlyList<WorkspaceRun>> ListAsync(CancellationToken token)
    {
        var runs = new List<WorkspaceRun>();
        foreach (var directory in Directory.EnumerateDirectories(_directory))
        {
            if (Guid.TryParse(Path.GetFileName(directory), out var id) && File.Exists(Path.Combine(directory, "run.json")))
                runs.Add(await ReadRunAsync(id, token));
        }
        return runs.OrderByDescending(run => run.StartedAt).ToArray();
    }

    public async Task<WorkspaceRunDetails> GetAsync(Guid id, CancellationToken token)
    {
        var run = await ReadRunAsync(id, token);
        var directory = RunDirectory(id);
        var logs = new List<string>();
        foreach (var name in new[] { "stdout.log", "stderr.log" })
        {
            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
                continue;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            logs.AddRange((await reader.ReadToEndAsync(token)).Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(300));
        }
        var metrics = new List<WorkspaceMetric>();
        var metricDirectory = Path.Combine(directory, "Metrics");
        if (!IsActive(run.State) && Directory.Exists(metricDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(metricDirectory, "*.json", SearchOption.AllDirectories))
            {
                if (Path.GetFileNameWithoutExtension(path).Contains("_Windowed_", StringComparison.Ordinal))
                    continue;
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    metrics.Add(new WorkspaceMetric($"{Path.GetFileName(Path.GetDirectoryName(path))}/{Path.GetFileNameWithoutExtension(path)}", root[root.GetArrayLength() - 1].Clone()));
            }
        }
        return new WorkspaceRunDetails(run, logs.OrderBy(line => line, StringComparer.Ordinal).TakeLast(300).ToArray(), metrics);
    }

    public async Task<WorkspaceRun> StartAsync(Guid planId, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_process != null)
                throw new InvalidOperationException("A run is already active. Stop it or wait for completion.");
            if (!File.Exists(_options.RunnerAssemblyPath))
                throw new InvalidOperationException("The LPS runner is not available.");

            var saved = await _plans.GetAsync(planId, token);
            var id = Guid.NewGuid();
            var directory = RunDirectory(id);
            Directory.CreateDirectory(directory);
            var planPath = Path.Combine(directory, "plan.json");
            await File.WriteAllTextAsync(planPath, SerializationHelper.Serialize(saved.Plan), token);
            var port = GetAvailablePort();
            var grpcPort = GetAvailablePort();
            while (grpcPort == port)
                grpcPort = GetAvailablePort();
            var settingsPath = await WriteSettingsAsync(directory, port, grpcPort, token);
            var run = new WorkspaceRun(id, planId, saved.Plan.Name, "Preparing", DateTimeOffset.UtcNow, null, null, port, saved.Plan);
            await SaveRunAsync(run, token);

            var start = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(_options.RunnerAssemblyPath);
            start.ArgumentList.Add("run");
            start.ArgumentList.Add(planPath);
            start.Environment["LPS_SETTINGS_FILE"] = settingsPath;
            start.Environment["LPS_WORKSPACE_RUN"] = directory;
            start.Environment["NO_COLOR"] = "1";
            var process = new Process { StartInfo = start };
            try
            {
                process.Start();
            }
            catch
            {
                process.Dispose();
                await SaveRunAsync(run with { State = "Failed", FinishedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
                throw;
            }
            _process = process;
            _activeRun = run;
            _stopRequested = false;
            _completion = MonitorAsync(process, run);
            return run;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkspaceRun> StopAsync(Guid id, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_activeRun?.Id != id || _process == null || _process.HasExited)
            {
                await ReadRunAsync(id, token);
                throw new InvalidOperationException("This run is no longer active.");
            }
            if (!_stopRequested)
            {
                await _process.StandardInput.WriteLineAsync("stop".AsMemory(), token);
                await _process.StandardInput.FlushAsync(token);
                _stopRequested = true;
                _activeRun = _activeRun with { State = "Stopping" };
                await SaveRunAsync(_activeRun, token);
            }
            return _activeRun;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MonitorAsync(Process process, WorkspaceRun run)
    {
        var directory = RunDirectory(run.Id);
        try
        {
            await Task.WhenAll(
                CaptureOutputAsync(process.StandardOutput, Path.Combine(directory, "stdout.log"), run.Plan),
                CaptureOutputAsync(process.StandardError, Path.Combine(directory, "stderr.log"), run.Plan),
                process.WaitForExitAsync());
            await _gate.WaitAsync();
            try
            {
                var failed = process.ExitCode != 0 || File.Exists(Path.Combine(directory, "failed"))
                    || !Directory.Exists(Path.Combine(directory, "Metrics"));
                var completed = run with
                {
                    State = _stopRequested ? "Cancelled" : failed ? "Failed" : "Completed",
                    FinishedAt = DateTimeOffset.UtcNow,
                    ExitCode = process.ExitCode
                };
                await SaveRunAsync(completed, CancellationToken.None);
                _activeRun = null;
                _process = null;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to retain workspace run {RunId}", run.Id);
            await _gate.WaitAsync();
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                _activeRun = null;
                _process = null;
                await SaveRunAsync(run with { State = "Failed", FinishedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task CaptureOutputAsync(StreamReader source, string path, PlanDto plan)
    {
        var secrets = plan.Rounds.SelectMany(round => round.Iterations).Concat(plan.Iterations)
            .SelectMany(iteration => iteration.HttpRequest.HttpHeaders ?? new Dictionary<string, string>())
            .Where(header => new[] { "authorization", "cookie", "key", "token", "secret" }
                .Any(word => header.Key.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .Select(header => header.Value).Where(value => !string.IsNullOrEmpty(value)).Distinct().ToArray();
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await using var writer = new StreamWriter(file) { AutoFlush = true };
        var written = 0;
        while (await source.ReadLineAsync() is { } line)
        {
            foreach (var secret in secrets)
                line = line.Replace(secret, "[redacted]", StringComparison.Ordinal);
            if (written < 2_000_000)
            {
                var text = $"{DateTimeOffset.UtcNow:O} {line[..Math.Min(line.Length, 4000)]}";
                await writer.WriteLineAsync(text);
                written += text.Length;
            }
        }
    }

    private async Task<WorkspaceRun> ReadRunAsync(Guid id, CancellationToken token)
    {
        var path = Path.Combine(RunDirectory(id), "run.json");
        if (!File.Exists(path))
            throw new KeyNotFoundException("Run not found.");
        var run = JsonSerializer.Deserialize<WorkspaceRun>(await File.ReadAllTextAsync(path, token), JsonOptions)
            ?? throw new InvalidDataException("The saved run is empty.");
        if (IsActive(run.State))
        {
            if (_activeRun?.Id != id)
                return run with { State = "Interrupted" };
            var statePath = Path.Combine(RunDirectory(id), "state");
            if (File.Exists(statePath))
            {
                var state = (await File.ReadAllTextAsync(statePath, token)).Trim();
                if (state == "Finalizing" || run.State != "Stopping" && state == "Running")
                    return run with { State = state };
            }
        }
        return run;
    }

    private async Task SaveRunAsync(WorkspaceRun run, CancellationToken token)
    {
        var path = Path.Combine(RunDirectory(run.Id), "run.json");
        var temporary = $"{path}.tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(run, JsonOptions), token);
        File.Move(temporary, path, overwrite: true);
    }

    private async Task<string> WriteSettingsAsync(string directory, int port, int grpcPort, CancellationToken token)
    {
        var settings = JsonNode.Parse(await File.ReadAllTextAsync(AppConstants.AppSettingsFileLocation, token))!;
        var app = settings["LPSAppSettings"]!;
        app["Dashboard"] = new JsonObject { ["BuiltInDashboard"] = false, ["Port"] = port, ["RefreshRate"] = 1 };
        app["Cluster"] = new JsonObject
        {
            ["MasterNodeIP"] = "127.0.0.1", ["GRPCPort"] = grpcPort,
            ["ExpectedNumberOfWorkers"] = 0, ["MasterNodeIsWorker"] = true
        };
        app["InfluxDB"] = new JsonObject { ["Enabled"] = false };
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, settings.ToJsonString(), token);
        return path;
    }

    private string RunDirectory(Guid id) => Path.Combine(_directory, id.ToString());

    internal static bool IsActive(string state) => state is "Preparing" or "Running" or "Stopping" or "Finalizing";

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    Task IHostedService.StartAsync(CancellationToken token) => Task.CompletedTask;

    async Task IHostedService.StopAsync(CancellationToken token)
    {
        var run = _activeRun;
        if (run == null)
            return;
        try
        {
            await StopAsync(run.Id, token);
            await _completion.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            if (_process is { HasExited: false } process)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            await _completion.WaitAsync(token);
        }
    }
}