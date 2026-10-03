using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.AccessControl;
using System.Security.Principal;
using LPS.Infrastructure.Distributed;
using LPS.Infrastructure.Nodes;

namespace LPS.UI.Core.Distributed;

internal static class DistributedFiles
{
    public static int AvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public static string Endpoint(string address)
    {
        var endpoint = ClusterRunSettings.ValidateEndpoint(address);
        return endpoint.Port == 0 ? new UriBuilder(endpoint) { Port = AvailablePort() }.Uri.GetLeftPart(UriPartial.Authority)
            : endpoint.GetLeftPart(UriPartial.Authority);
    }

    public static async Task WritePrivateAsync(string path, string content, CancellationToken token = default)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(security);
        }
        else File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content.AsMemory(), token);
    }

    public static async Task<string> WriteSettingsAsync(ClusterRunSettings run, string source, int dashboardPort, CancellationToken token)
    {
        var document = JsonNode.Parse(await File.ReadAllTextAsync(source, token))!.AsObject();
        var app = document["LPSAppSettings"]!.AsObject();
        var dashboard = app["Dashboard"] ??= new JsonObject();
        if (run.Role == NodeType.Worker) dashboard["BuiltInDashboard"] = false;
        dashboard["Port"] = dashboardPort == 0 ? AvailablePort() : dashboardPort;
        app["Cluster"] = new JsonObject
        {
            ["MasterNodeIP"] = "127.0.0.1", ["MasterNodePort"] = new Uri(run.MasterAddress).Port,
            ["ExpectedNumberOfWorkers"] = 0, ["MasterNodeIsWorker"] = true
        };
        if (run.Role == NodeType.Worker && app["InfluxDB"] is JsonObject influx) influx["Enabled"] = false;
        if (app["FileLogger"] is JsonObject logging)
        {
            logging["EnableConsoleLogging"] = false;
            logging["LogFilePath"] = "logs/lps.log";
        }
        document["Logging"] = new JsonObject { ["LogLevel"] = new JsonObject { ["Default"] = "Warning" } };
        var settingsPath = Path.Combine(run.RunDirectory, "settings.json");
        await WritePrivateAsync(settingsPath, document.ToJsonString(), token);
        await WritePrivateAsync(Path.Combine(run.RunDirectory, "run-settings.json"), JsonSerializer.Serialize(run), token);
        return settingsPath;
    }
}