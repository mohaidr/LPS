using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using LPS.UI.Core.Host;
using LPS.UI.Core.Web.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LPS.UI.Core.Web;

internal static class WebUiCommand
{
    public static async Task RunAsync(string[] args)
    {
        try
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                Console.WriteLine("Usage: lps ui [stop] [--port 8010] [--open-browser false] [--data-directory <path>] [--webroot <path>] [--foreground true]");
                return;
            }
            var stop = args.Length > 0 && args[0].Equals("stop", StringComparison.OrdinalIgnoreCase);
            if (stop) args = args[1..];
            if (args.Length > 0 && !args[0].StartsWith('-'))
                throw new ArgumentException("Unknown UI command. Use 'lps ui' or 'lps ui stop'.");
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;
            var port = WebUiHost.GetPort(configuration);
            var options = WebUiHost.GetOptions(configuration);
            if (!stop && configuration.GetValue("background-host", false))
            {
                await RunBackgroundHostAsync(builder, options, port);
                return;
            }
            if (!stop && configuration.GetValue("foreground", false))
            {
                await WebUiHost.RunAsync(builder);
                return;
            }

            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
                Timeout = TimeSpan.FromSeconds(5)
            };
            client.DefaultRequestHeaders.Add("X-LPS-Workspace", "1");
            var status = await GetHostAsync(client);
            if (status != null && configuration["data-directory"] != null)
                EnsureDataDirectory(status, options.DataDirectory);
            if (stop)
            {
                await StopAsync(client, status);
                return;
            }
            var alreadyRunning = status != null;
            if (status == null)
            {
                Directory.CreateDirectory(options.DataDirectory);
                using var process = Process.Start(CreateStartInfo(args, configuration, options, port))
                    ?? throw new InvalidOperationException("Could not start the UI host.");
                try
                {
                    var elapsed = Stopwatch.StartNew();
                    while (elapsed.Elapsed < TimeSpan.FromSeconds(30))
                    {
                        status = await GetHostAsync(client);
                        if (status != null) break;
                        if (process.HasExited)
                            throw new InvalidOperationException($"The UI host could not start. See {LogPath(options, port)}");
                        await Task.Delay(150);
                    }
                    if (status == null)
                        throw new TimeoutException($"The UI host did not become ready. See {LogPath(options, port)}");
                    EnsureDataDirectory(status, options.DataDirectory);
                    if (status.ProcessId != process.Id && !process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    throw;
                }
            }
            if (status.IsStopping)
                throw new InvalidOperationException("The UI host is stopping. Retry once shutdown finishes.");
            var url = $"{client.BaseAddress}workspace";
            Console.WriteLine($"LPS UI {(alreadyRunning ? "already running" : "started")} at {url}");
            if (configuration.GetValue("open-browser", true)) DashboardService.OpenBrowser(url);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"LPS UI: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string[] args, IConfiguration configuration, WorkspaceOptions options, int port)
    {
        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(options.RunnerAssemblyPath);
        start.ArgumentList.Add("ui");
        foreach (var argument in args) start.ArgumentList.Add(argument);
        foreach (var argument in new[] { "--background-host", "true", "--open-browser", "false", "--port", port.ToString(), "--data-directory", options.DataDirectory })
            start.ArgumentList.Add(argument);
        if (configuration["webroot"] is { } webRoot)
        {
            start.ArgumentList.Add("--webroot");
            start.ArgumentList.Add(Path.GetFullPath(webRoot));
        }
        return start;
    }

    private static async Task<WorkspaceHostStatus?> GetHostAsync(HttpClient client)
    {
        try
        {
            using var response = await client.GetAsync("api/workspace/host");
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync<WorkspaceHostStatus>();
                if (status?.Application == WorkspaceHostStatus.ApplicationName && status.ProcessId > 0 && status.InstanceId != Guid.Empty)
                    return status;
            }
        }
        catch (HttpRequestException exception) when (exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        {
            return null;
        }
        catch (JsonException) { }
        throw new InvalidOperationException($"Port {client.BaseAddress!.Port} is not serving a compatible LPS UI host. Stop the older host or choose another --port.");
    }

    private static void EnsureDataDirectory(WorkspaceHostStatus status, string directory)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!Path.TrimEndingDirectorySeparator(Path.GetFullPath(status.DataDirectory)).Equals(Path.TrimEndingDirectorySeparator(directory), comparison))
            throw new InvalidOperationException("The UI host on this port uses another data directory. Choose another --port or stop that host first.");
    }

    private static async Task StopAsync(HttpClient client, WorkspaceHostStatus? status)
    {
        if (status == null)
        {
            Console.WriteLine($"No LPS UI host is running at {client.BaseAddress}");
            return;
        }
        Process process;
        try { process = Process.GetProcessById(status.ProcessId); }
        catch (ArgumentException) { Console.WriteLine("LPS UI is already stopped."); return; }
        using (process)
        {
            if (!status.IsStopping)
            {
                using var response = await client.PostAsync($"api/workspace/host/stop?instanceId={status.InstanceId}", null);
                response.EnsureSuccessStatusCode();
            }
            Console.WriteLine("Stopping LPS UI. Waiting for any active test to finalize...");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { throw new TimeoutException("Shutdown is taking longer than expected. The host has not been force-killed."); }
        }
        Console.WriteLine("LPS UI stopped.");
    }

    private static string LogPath(WorkspaceOptions options, int port) => Path.Combine(options.DataDirectory, $"ui-{port}.log");

    private static async Task RunBackgroundHostAsync(WebApplicationBuilder builder, WorkspaceOptions options, int port)
    {
        Directory.CreateDirectory(options.DataDirectory);
        await using var file = new FileStream(LogPath(options, port), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        await using var writer = new StreamWriter(file) { AutoFlush = true };
        var output = Console.Out;
        var error = Console.Error;
        var synchronized = TextWriter.Synchronized(writer);
        Console.SetOut(synchronized);
        Console.SetError(synchronized);
        using var hangup = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGHUP, context => context.Cancel = true);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        try { await WebUiHost.RunAsync(builder); }
        catch (Exception exception) { Console.Error.WriteLine(exception); Environment.ExitCode = 1; }
        finally { Console.SetOut(output); Console.SetError(error); }
    }
}