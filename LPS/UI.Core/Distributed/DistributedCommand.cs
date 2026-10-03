using System.CommandLine;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Distributed;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common.DTOs;
using LPS.UI.Common.Options;
using Microsoft.Extensions.Configuration;

namespace LPS.UI.Core.Distributed;

internal static class DistributedCommand
{
    public static async Task RunAsync(string[] args)
    {
        var worker = args[0].Equals("worker", StringComparison.OrdinalIgnoreCase);
        var root = new RootCommand(worker ? "Register a persistent worker agent and wait for assigned plans." : "Distribute a plan to isolated worker agents.");
        var plan = new Argument<string>("plan", "JSON or YAML plan path");
        var master = new Option<string?>("--master", "Master gRPC address; defaults to Cluster.MasterNodeIP and MasterNodePort (GRPCPort is also accepted)");
        var name = new Option<string>("--name", () => worker ? Environment.MachineName : "master", "Unique worker name");
        var listen = new Option<string?>("--listen", "Own callback endpoint; master defaults to Cluster settings, worker to its detected address and an automatic port");
        var directory = new Option<string?>("--data-directory", "Private directory for run configuration, logs, and results");
        var source = new Option<string>("--settings", () => Environment.GetEnvironmentVariable("LPS_SETTINGS_FILE")
            ?? Path.Combine(AppContext.BaseDirectory, "config", "lpsSettings.json"), "Source LPS settings file; never modified");
        var certificate = new Option<string?>("--certificate", "Server PFX certificate; password from LPS_CLUSTER_CERT_PASSWORD");
        var authority = new Option<string?>("--ca-cert", "Optional trusted cluster CA PEM; hostname checks remain enabled");
        var workers = new Option<int?>("--workers", "Required external workers; defaults to Cluster.ExpectedNumberOfWorkers or 1");
        var masterIsWorker = new Option<bool?>("--masternodeisworker", "Also execute locally; defaults to Cluster.MasterNodeIsWorker or false");
        masterIsWorker.AddAlias("-miw");
        var timeout = new Option<int>("--registration-timeout", () => 60, "Registration and preparation timeout in seconds");
        var lease = new Option<int>("--lease-seconds", () => 10, "Heartbeat lease, between 3 and 30 seconds");
        var dashboard = new Option<int>("--dashboard-port", () => 0, "Master dashboard port; 0 allocates a port");
        foreach (var option in new Option[] { name, listen, directory, source, certificate, authority }) root.AddOption(option);
        if (worker) root.AddOption(master);
        else
        {
            root.AddArgument(plan);
            foreach (var option in new Option[] { workers, masterIsWorker, timeout, lease, dashboard }) root.AddOption(option);
        }
        root.SetHandler(async context =>
        {
            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
            Console.CancelKeyPress += cancel;
            _ = Task.Run(async () =>
            {
                if (!Console.IsInputRedirected) return;
                try
                {
                    while (await Console.In.ReadLineAsync(cancellation.Token) is { } command)
                        if (command == "stop") { cancellation.Cancel(); return; }
                    cancellation.Cancel();
                }
                catch (OperationCanceledException) { }
            });
            try
            {
                var parsed = context.ParseResult;
                var nodeName = parsed.GetValueForOption(name)!;
                if (!Regex.IsMatch(nodeName, "^[A-Za-z0-9_-]{1,64}$")) throw new ArgumentException("Use 1-64 letters, digits, underscores, or hyphens for --name.");
                if (worker && nodeName == MasterCoordinator.LocalWorkerName)
                    throw new ArgumentException($"Worker name '{MasterCoordinator.LocalWorkerName}' is reserved for master load execution.");
                var settingsPath = Path.GetFullPath(parsed.GetValueForOption(source)!);
                if (!File.Exists(settingsPath)) throw new FileNotFoundException("LPS settings file was not found; use --settings.");
                using var configuration = new ConfigurationManager();
                configuration.AddJsonFile(settingsPath);
                var cluster = configuration.GetSection("LPSAppSettings:Cluster").Get<ClusterConfigurationOptions>();
                var masterAddress = worker ? ResolveMasterEndpoint(parsed.GetValueForOption(master), cluster) : null;
                var endpoint = ClusterRunSettings.ValidateEndpoint(worker
                    ? ResolveWorkerEndpoint(parsed.GetValueForOption(listen), masterAddress)
                    : parsed.GetValueForOption(listen) ?? ResolveMasterEndpoint(null, cluster) ?? "http://127.0.0.1:0");
                var token = Environment.GetEnvironmentVariable("LPS_CLUSTER_TOKEN");
                if (string.IsNullOrWhiteSpace(token) || token.Length < 32 || token.Any(char.IsWhiteSpace))
                    throw new ArgumentException("Set LPS_CLUSTER_TOKEN to a private random token of at least 32 characters on the master and workers.");
                var certificatePath = parsed.GetValueForOption(certificate) is { } cert ? Path.GetFullPath(cert) : null;
                var authorityPath = parsed.GetValueForOption(authority) is { } ca ? Path.GetFullPath(ca) : null;
                if (endpoint.Scheme == "https" && (certificatePath == null || !File.Exists(certificatePath)))
                    throw new ArgumentException("HTTPS listeners require --certificate pointing to a server PFX file.");
                if (authorityPath != null && !File.Exists(authorityPath)) throw new FileNotFoundException("Cluster CA file was not found.");
                var dataRoot = Path.GetFullPath(parsed.GetValueForOption(directory)
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LPS", worker ? "Workers" : "Masters", nodeName));
                if (worker)
                {
                    var masterEndpoint = masterAddress ?? throw new ArgumentException("Specify --master or configure LPSAppSettings.Cluster.MasterNodeIP and MasterNodePort.");
                    if (!new Uri(masterEndpoint).IsLoopback && endpoint.IsLoopback)
                        throw new ArgumentException("A remote master requires a reachable HTTPS worker address, not localhost.");
                    Console.WriteLine($"Worker {nodeName}: connecting to {masterEndpoint}. Ctrl+C stops the agent and its active run.");
                    await new WorkerAgent(new WorkerAgentOptions
                    {
                        Name = nodeName, MasterAddress = masterEndpoint, ListenAddress = endpoint.GetLeftPart(UriPartial.Authority),
                        DataDirectory = dataRoot, SettingsPath = settingsPath, Token = token, CertificatePath = certificatePath,
                        CertificatePassword = Environment.GetEnvironmentVariable("LPS_CLUSTER_CERT_PASSWORD"), CertificateAuthorityPath = authorityPath
                    }).RunAsync(cancellation.Token);
                    context.ExitCode = Environment.ExitCode;
                }
                else
                {
                    var expected = parsed.GetValueForOption(workers) ?? cluster?.ExpectedNumberOfWorkers ?? 1;
                    var executeLocally = parsed.GetValueForOption(masterIsWorker) ?? cluster?.MasterNodeIsWorker ?? false;
                    var totalWorkers = expected + (executeLocally ? 1 : 0);
                    var waitSeconds = parsed.GetValueForOption(timeout);
                    var leaseSeconds = parsed.GetValueForOption(lease);
                    var dashboardPort = parsed.GetValueForOption(dashboard);
                    if (expected < 0 || totalWorkers is < 1 or > 256 || waitSeconds is < 1 or > 3600 || leaseSeconds is < 3 or > 30 || dashboardPort is < 0 or > 65535)
                        throw new ArgumentException("Invalid worker count, timeout, lease, or dashboard port.");
                    var planPath = Path.GetFullPath(parsed.GetValueForArgument(plan));
                    var content = await File.ReadAllTextAsync(planPath, cancellation.Token);
                    var planDto = Path.GetExtension(planPath).Equals(".json", StringComparison.OrdinalIgnoreCase)
                        ? SerializationHelper.Deserialize<PlanDto>(content) : SerializationHelper.DeserializeFromYaml<PlanDto>(content);
                    var runId = Guid.NewGuid().ToString();
                    var runDirectory = Path.Combine(dataRoot, runId);
                    var address = DistributedFiles.Endpoint(endpoint.GetLeftPart(UriPartial.Authority));
                    var run = new ClusterRunSettings
                    {
                        RunId = runId, NodeName = nodeName, RunDirectory = runDirectory, PlanPath = Path.Combine(runDirectory, "plan.json"),
                        Role = NodeType.Master, NodeAddress = address, MasterAddress = address, Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                        RegistrationToken = token, CertificatePath = certificatePath, CertificatePassword = Environment.GetEnvironmentVariable("LPS_CLUSTER_CERT_PASSWORD"),
                        CertificateAuthorityPath = authorityPath, ExpectedWorkers = totalWorkers, MasterNodeIsWorker = executeLocally,
                        RegistrationTimeoutSeconds = waitSeconds, LeaseSeconds = leaseSeconds
                    };
                    await DistributedFiles.WritePrivateAsync(run.PlanPath, SerializationHelper.Serialize(planDto), cancellation.Token);
                    var copied = await DistributedFiles.WriteSettingsAsync(run, settingsPath, dashboardPort, cancellation.Token);
                    Console.WriteLine($"Master endpoint: {address}");
                    Console.WriteLine($"Private run directory: {runDirectory}");
                    await using var process = new IsolatedRunProcess(run, copied, true);
                    try { await process.Exited.WaitAsync(cancellation.Token); }
                    catch (OperationCanceledException) { await process.StopAsync(); }
                    context.ExitCode = cancellation.IsCancellationRequested ? 1 : process.ExitCode;
                }
            }
            catch (OperationCanceledException) { context.ExitCode = 1; }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"LPS distributed command: {exception.Message}");
                context.ExitCode = 1;
            }
            finally
            {
                cancellation.Cancel();
                Console.CancelKeyPress -= cancel;
            }
        });
        Environment.ExitCode = await root.InvokeAsync(args[1..]);
    }

    internal static string ResolveWorkerEndpoint(string? address, string? masterAddress, string? localAddress = null)
    {
        if (address != null) return ClusterRunSettings.ValidateEndpoint(address).GetLeftPart(UriPartial.Authority);
        if (masterAddress == null || new Uri(masterAddress).IsLoopback) return "http://127.0.0.1:0";
        return new UriBuilder("https", localAddress ?? INode.NodeIP, 0).Uri.GetLeftPart(UriPartial.Authority);
    }

    internal static string? ResolveMasterEndpoint(string? address, ClusterConfigurationOptions? cluster)
    {
        if (address != null) return ClusterRunSettings.ValidateEndpoint(address).GetLeftPart(UriPartial.Authority);
        if (string.IsNullOrWhiteSpace(cluster?.MasterNodeIP)) return null;
        var host = cluster.MasterNodeIP.Trim();
        if (host.Contains("://")) return ClusterRunSettings.ValidateEndpoint(host).GetLeftPart(UriPartial.Authority);
        if (cluster.MasterNodePort is not (>= 1 and <= 65535))
            throw new ArgumentException("Configure Cluster.MasterNodePort (1-65535) or use a full HTTP(S) master address.");
        var endpoint = new UriBuilder(Uri.UriSchemeHttp, host, cluster.MasterNodePort.Value);
        if (!endpoint.Uri.IsLoopback) endpoint.Scheme = Uri.UriSchemeHttps;
        return ClusterRunSettings.ValidateEndpoint(endpoint.Uri.AbsoluteUri).GetLeftPart(UriPartial.Authority);
    }
}