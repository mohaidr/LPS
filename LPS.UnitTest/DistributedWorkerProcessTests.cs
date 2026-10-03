using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using LPS.Infrastructure.Common;
using LPS.UI.Common.DTOs;
using LPS.UI.Core.Distributed;
using Xunit.Abstractions;
using Grpc.Core;
using LPS.Infrastructure.Distributed;
using LPS.Protos.Shared;
using System.Text.Json.Nodes;

namespace LPS.UnitTest;

public class DistributedWorkerProcessTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [Trait("Category", "DistributedProcess")]
    public async Task LegacyWorkers_AdvertiseIndependentPorts(bool canonicalSetting, bool explicitPort, bool occupiedPort)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-distributed-tests", Guid.NewGuid().ToString("N"));
        var masterDirectory = Path.Combine(directory, "master");
        var workerDirectory = Path.Combine(directory, "worker");
        Directory.CreateDirectory(masterDirectory);
        Directory.CreateDirectory(workerDirectory);
        output.WriteLine($"Retained artifacts: {directory}");
        var masterPort = DistributedFiles.AvailablePort();
        var workerPort = DistributedFiles.AvailablePort();
        var targetAddress = $"http://127.0.0.1:{DistributedFiles.AvailablePort()}";
        using var occupied = new TcpListener(IPAddress.Parse("127.0.0.2"), workerPort) { ExclusiveAddressUse = true };
        if (occupiedPort) occupied.Start();
        var settings = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "config", "lpsSettings.json")))!;
        settings["LPSAppSettings"]!["Dashboard"]!["BuiltInDashboard"] = false;
        settings["LPSAppSettings"]!["Dashboard"]!["Port"] = 0;
        settings["LPSAppSettings"]!["Cluster"] = new JsonObject
        {
            ["MasterNodeIP"] = "127.0.0.1", [canonicalSetting ? "MasterNodePort" : "GRPCPort"] = masterPort,
            ["ExpectedNumberOfWorkers"] = 1, ["MasterNodeIsWorker"] = false
        };
        var source = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(source, settings.ToJsonString());
        var plan = Path.Combine(directory, "plan.json");
        await File.WriteAllTextAsync(plan, JsonSerializer.Serialize(new
        {
            Name = "Legacy endpoints", Rounds = new[] { new
            {
                Name = "Round", NumberOfClients = "1", Iterations = new[] { new
                {
                    Name = "Request", Mode = "R", RequestCount = "10",
                    HttpRequest = new { URL = targetAddress + "/load", HttpMethod = "GET" }
                } }
            } }
        }));
        using var target = new HttpListener();
        target.Prefixes.Add(targetAddress + "/");
        target.Start();
        var requests = 0;
        var server = Task.Run(async () =>
        {
            try
            {
                while (target.IsListening)
                {
                    var context = await target.GetContextAsync();
                    if (context.Request.HttpMethod == "GET" && context.Request.Url!.AbsolutePath == "/load")
                        Interlocked.Increment(ref requests);
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException) when (!target.IsListening) { }
            catch (ObjectDisposedException) { }
        });
        var processes = new List<(Process Process, Task<string> Output, Task<string> Error, string Name)>();
        try
        {
            var master = Start("legacy-master", masterDirectory, new string('x', 64), sourceSettings: source,
                arguments: ["run", plan, "--listen", $"http://127.0.0.1:{masterPort}"]);
            processes.Add(master);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var channel = ClusterGrpcTransport.CreateChannel($"http://127.0.0.1:{masterPort}");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (!ready.Task.IsCompleted)
            {
                try
                {
                    var status = await new NodeService.NodeServiceClient(channel).GetNodeStatusAsync(new GetNodeStatusRequest(), cancellationToken: timeout.Token);
                    if (status.Status == LPS.Protos.Shared.NodeStatus.Ready) ready.TrySetResult();
                }
                catch (RpcException) when (!timeout.IsCancellationRequested) { }
                if (!ready.Task.IsCompleted) await timer.WaitForNextTickAsync(timeout.Token);
            }
            var worker = Start("legacy-worker", workerDirectory, new string('x', 64), sourceSettings: source,
                arguments: ["run", plan, ..(explicitPort ? new[] { "--listen", $"http://127.0.0.2:{workerPort}" } : Array.Empty<string>())]);
            processes.Add(worker);
            await worker.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            if (occupiedPort)
            {
                Assert.NotEqual(0, worker.Process.ExitCode);
                Assert.Equal(0, Volatile.Read(ref requests));
                return;
            }
            await master.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            Assert.Equal(0, worker.Process.ExitCode);
            Assert.Equal(0, master.Process.ExitCode);
            Assert.Equal(10, Volatile.Read(ref requests));
            var workerOutput = await worker.Output;
            var endpointLine = Assert.Single(workerOutput.Split('\n').Where(line => line.StartsWith("Node endpoint: ")));
            var endpoint = new Uri(endpointLine["Node endpoint: ".Length..].Trim());
            Assert.NotEqual(masterPort, endpoint.Port);
            Assert.NotEqual(0, endpoint.Port);
            if (explicitPort) Assert.Equal(workerPort, endpoint.Port);
            Assert.Contains("Received TriggerTest request", workerOutput);
            Assert.Contains("Finalization complete.", workerOutput);
        }
        finally
        {
            foreach (var process in processes)
            {
                if (!process.Process.HasExited) { process.Process.Kill(entireProcessTree: true); await process.Process.WaitForExitAsync(); }
                await StopAsync(process, directory);
            }
            target.Stop();
            await server;
        }
    }

    [Theory]
    [InlineData(false, false, false, false, 2)]
    [InlineData(true, false, false, false, 2)]
    [InlineData(false, true, false, false, 1)]
    [InlineData(false, false, true, false, 2)]
    [InlineData(false, false, true, true, 0)]
    [InlineData(false, false, true, true, 1)]
    [InlineData(true, false, true, true, 1)]
    [InlineData(false, true, true, true, 1)]
    [InlineData(false, false, false, true, 1)]
    [Trait("Category", "DistributedProcess")]
    public async Task Agents_ExecuteIsolatedRuns_WithFinalMetrics(bool tls, bool failureRule, bool fromSettings, bool masterIsWorker, int workerCount)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-distributed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        output.WriteLine($"Retained artifacts: {directory}");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var masterAddress = $"{(tls ? "https" : "http")}://127.0.0.1:{DistributedFiles.AvailablePort()}";
        var certificateArguments = tls ? CreateCertificate(directory) : [];
        var totalWorkers = workerCount + (masterIsWorker ? 1 : 0);
        var runCount = failureRule ? 1 : 2;
        var targetAddress = $"http://127.0.0.1:{DistributedFiles.AvailablePort()}";
        var source = Path.Combine(AppContext.BaseDirectory, "config", "lpsSettings.json");
        Assert.True(File.Exists(source), $"Missing settings template: {source}");
        var configured = JsonNode.Parse(await File.ReadAllTextAsync(source))!;
        configured["LPSAppSettings"]!["Cluster"] = new JsonObject
        {
            ["MasterNodeIP"] = fromSettings ? (tls ? masterAddress : "127.0.0.1") : "http://127.0.0.1:1",
            [tls ? "MasterNodePort" : "GRPCPort"] = new Uri(masterAddress).Port,
            ["ExpectedNumberOfWorkers"] = fromSettings ? workerCount : 9,
            ["MasterNodeIsWorker"] = fromSettings ? masterIsWorker : !masterIsWorker
        };
        source = Path.Combine(directory, "source.json");
        await File.WriteAllTextAsync(source, configured.ToJsonString());
        var originalSettings = await File.ReadAllTextAsync(source);
        var processes = new List<(Process Process, Task<string> Output, Task<string> Error, string Name)>();
        using var target = new HttpListener();
        target.Prefixes.Add(targetAddress + "/");
        target.Start();
        var requests = 0;
        var server = Task.Run(async () =>
        {
            try
            {
                while (target.IsListening)
                {
                    var request = await target.GetContextAsync();
                    if (request.Request.HttpMethod == "GET" && request.Request.Url!.AbsolutePath == "/load")
                        Interlocked.Increment(ref requests);
                    request.Response.StatusCode = failureRule ? 500 : 200;
                    request.Response.ContentLength64 = 2;
                    await request.Response.OutputStream.WriteAsync("ok"u8.ToArray());
                    request.Response.Close();
                }
            }
            catch (HttpListenerException) when (!target.IsListening) { }
            catch (ObjectDisposedException) { }
        });
        try
        {
            var plan = new PlanDto
            {
                Name = "Distributed process check",
                Rounds = [new RoundDto
                {
                    Name = "Round", NumberOfClients = "1", RunClientsSequentially = "true",
                    Iterations = [new HttpIterationDto
                    {
                        Name = "Request", Mode = "R", RequestCount = "10",
                        HttpRequest = new HttpRequestDto { URL = targetAddress + "/load", HttpMethod = "GET" }
                    }]
                }]
            };
            var planPath = Path.Combine(directory, "plan.json");
            await File.WriteAllTextAsync(planPath, SerializationHelper.Serialize(plan));
            for (var index = 1; index <= workerCount; index++)
                processes.Add(Start($"worker-{index}", directory, token, ["worker", "--name", $"worker-{index}",
                    ..(fromSettings ? Array.Empty<string>() : new[] { "--master", masterAddress }),
                    "--listen", tls ? "https://127.0.0.1:0" : "http://127.0.0.1:0",
                    "--settings", source, "--data-directory", Path.Combine(directory, $"worker-{index}"), ..certificateArguments]));
            for (var run = 1; run <= runCount; run++)
            {
                var master = Start($"master-{run}", directory, token, ["master", planPath,
                    ..(fromSettings ? Array.Empty<string>() : new[] { "--listen", masterAddress, "--workers", workerCount.ToString(), "--masternodeisworker", masterIsWorker.ToString() }),
                    "--registration-timeout", "30", "--settings", source, "--data-directory", Path.Combine(directory, $"master-{run}"), ..certificateArguments]);
                processes.Add(master);
                await master.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(100));
                Assert.True(master.Process.ExitCode == (failureRule ? 1 : 0), $"Master exited {master.Process.ExitCode}: {await master.Output}\n{await master.Error}\nArtifacts: {directory}");
                Assert.Equal(run * totalWorkers * 10, Volatile.Read(ref requests));
                var resultPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, $"master-{run}"), "cluster-result.json", SearchOption.AllDirectories));
                using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
                Assert.Equal(failureRule ? "Failed" : "Completed", result.RootElement.GetProperty("State").GetString());
                var workers = result.RootElement.GetProperty("Workers").EnumerateArray().ToArray();
                Assert.Equal(totalWorkers, workers.Length);
                Assert.All(workers, worker => Assert.Equal("Completed", worker.GetProperty("State").GetString()));
                Assert.Equal(totalWorkers, workers.Select(worker => worker.GetProperty("Endpoint").GetString()).Distinct().Count());
                if (masterIsWorker)
                {
                    Assert.Single(workers.Where(worker => worker.GetProperty("WorkerId").GetString() == "master-local"));
                    var completion = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(resultPath)!, "completion.json", SearchOption.AllDirectories));
                    using var processInfo = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(completion)!, "process.json")));
                    Assert.Throws<ArgumentException>(() => Process.GetProcessById(processInfo.RootElement.GetProperty("ProcessId").GetInt32()));
                }
                var metricsPath = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(resultPath)!, "Request_Throughput.json", SearchOption.AllDirectories));
                using var metrics = JsonDocument.Parse(await File.ReadAllTextAsync(metricsPath));
                var final = Assert.Single(metrics.RootElement.EnumerateArray().Where(snapshot => snapshot.GetProperty("IsFinal").GetBoolean()));
                Assert.Equal(totalWorkers * 10, final.GetProperty("Metric").GetProperty("RequestsCount").GetInt32());
                Assert.Equal(failureRule ? 0 : totalWorkers * 10, final.GetProperty("Metric").GetProperty("SuccessfulRequestCount").GetInt32());
                Assert.Equal(failureRule ? "Failed" : "Success", final.GetProperty("ExecutionStatus").GetString());
                Assert.All(processes.Take(workerCount), worker => Assert.False(worker.Process.HasExited));
                Assert.Equal(originalSettings, await File.ReadAllTextAsync(source));
            }
            foreach (var worker in processes.Take(workerCount))
            {
                var completions = Directory.GetFiles(Path.Combine(directory, worker.Name), "completion.json", SearchOption.AllDirectories);
                Assert.Equal(runCount, completions.Length);
                foreach (var completion in completions)
                {
                    using var result = JsonDocument.Parse(await File.ReadAllTextAsync(completion));
                    Assert.Equal("Completed", result.RootElement.GetProperty("State").GetString());
                }
            }
        }
        finally
        {
            foreach (var process in processes) await StopAsync(process, directory);
            target.Stop();
            await server;
        }
    }

    [Theory]
    [InlineData("cancel", false)]
    [InlineData("master-lost", false)]
    [InlineData("worker-lost", false)]
    [InlineData("cancel", true)]
    [InlineData("master-child-lost", true)]
    [InlineData("worker-lost", true)]
    [Trait("Category", "DistributedProcess")]
    public async Task InterruptedRun_StopsExecutor_WithoutReplay(string interruption, bool masterIsWorker)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-distributed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        output.WriteLine($"Retained artifacts: {directory}");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var masterAddress = $"http://127.0.0.1:{DistributedFiles.AvailablePort()}";
        var targetAddress = $"http://127.0.0.1:{DistributedFiles.AvailablePort()}";
        var firstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var target = new HttpListener();
        target.Prefixes.Add(targetAddress + "/");
        target.Start();
        var requests = 0;
        var stoppingTarget = false;
        var server = Task.Run(async () =>
        {
            try
            {
                while (target.IsListening)
                {
                    var context = await target.GetContextAsync();
                    if (context.Request.HttpMethod == "GET" && context.Request.Url!.AbsolutePath == "/load")
                    {
                        Interlocked.Increment(ref requests);
                        firstRequest.TrySetResult();
                        await Task.Delay(100);
                    }
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException) when (Volatile.Read(ref stoppingTarget) || !target.IsListening) { }
            catch (ObjectDisposedException) { }
        });
        var planPath = Path.Combine(directory, "plan.json");
        await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(new
        {
            Name = "Interrupted run", Rounds = new[] { new
            {
                Name = "Round", NumberOfClients = "1", Iterations = new[] { new
                {
                    Name = "Request", Mode = "R", RequestCount = "100",
                    HttpRequest = new { URL = targetAddress + "/load", HttpMethod = "GET" }
                } }
            } }
        }));
        var source = Path.Combine(AppContext.BaseDirectory, "config", "lpsSettings.json");
        var worker = Start("worker", directory, token, "worker", "--name", "worker-1", "--master", masterAddress,
            "--settings", source, "--data-directory", Path.Combine(directory, "worker"));
        var master = Start("master", directory, token, "master", planPath, "--listen", masterAddress, "--workers", "1",
            "--masternodeisworker", masterIsWorker.ToString(),
            "--lease-seconds", "3", "--settings", source, "--data-directory", Path.Combine(directory, "master"));
        Process? localExecutor = null;
        try
        {
            await firstRequest.Task.WaitAsync(TimeSpan.FromSeconds(35));
            var executorPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "worker"), "process.json", SearchOption.AllDirectories));
            using var executorInfo = JsonDocument.Parse(await File.ReadAllTextAsync(executorPath));
            using var executor = Process.GetProcessById(executorInfo.RootElement.GetProperty("ProcessId").GetInt32());
            Assert.NotEqual(worker.Process.Id, executor.Id);
            if (masterIsWorker)
            {
                var localProcessPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "master"), "process.json", SearchOption.AllDirectories)
                    .Where(path => path.Contains($"{Path.DirectorySeparatorChar}local-worker{Path.DirectorySeparatorChar}")));
                using var localProcessInfo = JsonDocument.Parse(await File.ReadAllTextAsync(localProcessPath));
                localExecutor = Process.GetProcessById(localProcessInfo.RootElement.GetProperty("ProcessId").GetInt32());
            }
            if (interruption == "cancel") await master.Process.StandardInput.WriteLineAsync("stop");
            else if (interruption == "master-lost") master.Process.Kill(entireProcessTree: true);
            else if (interruption == "master-child-lost")
            {
                var masterProcessPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "master"), "process.json", SearchOption.AllDirectories)
                    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}local-worker{Path.DirectorySeparatorChar}")));
                using var masterProcessInfo = JsonDocument.Parse(await File.ReadAllTextAsync(masterProcessPath));
                using var coordinator = Process.GetProcessById(masterProcessInfo.RootElement.GetProperty("ProcessId").GetInt32());
                coordinator.Kill();
            }
            else worker.Process.Kill();
            await executor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(55));
            if (localExecutor != null) await localExecutor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(55));
            await master.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(55));
            Assert.NotEqual(0, master.Process.ExitCode);
            Assert.InRange(Volatile.Read(ref requests), 1, masterIsWorker ? 199 : 99);
            Assert.Single(Directory.GetFiles(Path.Combine(directory, "worker"), "claimed", SearchOption.AllDirectories));
            if (interruption != "worker-lost") Assert.False(worker.Process.HasExited);
            if (interruption is not ("master-lost" or "master-child-lost"))
            {
                var resultPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "master"), "cluster-result.json", SearchOption.AllDirectories));
                using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
                Assert.NotEqual("Completed", result.RootElement.GetProperty("State").GetString());
            }
        }
        finally
        {
            await StopAsync(master, directory);
            await StopAsync(worker, directory);
            localExecutor?.Dispose();
            Volatile.Write(ref stoppingTarget, true);
            target.Stop();
            await server;
        }
    }

    private static async Task StopAsync((Process Process, Task<string> Output, Task<string> Error, string Name) process, string directory)
    {
        if (!process.Process.HasExited)
        {
            await process.Process.StandardInput.WriteLineAsync("stop");
            await process.Process.StandardInput.FlushAsync();
            try { await process.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(35)); }
            catch (TimeoutException) { process.Process.Kill(entireProcessTree: true); await process.Process.WaitForExitAsync(); }
        }
        await File.WriteAllTextAsync(Path.Combine(directory, process.Name + ".stdout.log"), await process.Output);
        await File.WriteAllTextAsync(Path.Combine(directory, process.Name + ".stderr.log"), await process.Error);
        process.Process.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "DistributedProcess")]
    public async Task Registration_RejectsBadCredentials_Versions_AndDuplicateIdentities(bool tls)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-distributed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        output.WriteLine($"Retained artifacts: {directory}");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var address = $"{(tls ? "https" : "http")}://127.0.0.1:{DistributedFiles.AvailablePort()}";
        var certificateArguments = tls ? CreateCertificate(directory) : [];
        var authority = tls ? Path.Combine(directory, "ca.pem") : null;
        var plan = Path.Combine(directory, "plan.json");
        await File.WriteAllTextAsync(plan, """
            {"Name":"Admission","Rounds":[{"Name":"Round","NumberOfClients":"1","Iterations":[{"Name":"Request","RequestCount":"1","HttpRequest":{"URL":"http://127.0.0.1:1/load","HttpMethod":"GET"}}]}]}
            """);
        var master = Start("master", directory, token, ["master", plan, "--listen", address, "--workers", "2", "--settings",
            Path.Combine(AppContext.BaseDirectory, "config", "lpsSettings.json"), "--data-directory", Path.Combine(directory, "master"), ..certificateArguments]);
        try
        {
            await WaitUntilAsync(() => Directory.GetFiles(directory, "ready", SearchOption.AllDirectories).Length > 0);
            WorkerUpdate Hello(string name = "worker-1", string endpoint = "http://127.0.0.1:51001") => new()
            {
                WorkerId = name, SessionId = Guid.NewGuid().ToString(), Endpoint = endpoint,
                State = WorkerRunState.Idle, ProtocolVersion = MasterCoordinator.ProtocolVersion, LpsVersion = MasterCoordinator.Version
            };
            async Task RejectAsync(string secret, WorkerUpdate hello, StatusCode expected)
            {
                using var channel = ClusterGrpcTransport.CreateChannel(address, token: secret, certificateAuthorityPath: authority);
                using var call = new WorkerControl.WorkerControlClient(channel).Connect(deadline: DateTime.UtcNow.AddSeconds(5));
                await call.RequestStream.WriteAsync(hello);
                var exception = await Assert.ThrowsAsync<RpcException>(() => call.ResponseStream.MoveNext(CancellationToken.None));
                Assert.Equal(expected, exception.StatusCode);
            }
            await RejectAsync(new string('x', 64), Hello(), StatusCode.Unauthenticated);
            if (tls)
            {
                using var untrusted = ClusterGrpcTransport.CreateChannel(address, token: token);
                using var call = new WorkerControl.WorkerControlClient(untrusted).Connect(deadline: DateTime.UtcNow.AddSeconds(5));
                var exception = await Assert.ThrowsAsync<RpcException>(async () =>
                {
                    await call.RequestStream.WriteAsync(Hello());
                    await call.ResponseStream.MoveNext(CancellationToken.None);
                });
                Assert.Contains(exception.StatusCode, new[] { StatusCode.Unavailable, StatusCode.Internal });
                Assert.Contains("SSL", exception.Status.Detail, StringComparison.OrdinalIgnoreCase);
            }
            var incompatible = Hello();
            incompatible.ProtocolVersion++;
            await RejectAsync(token, incompatible, StatusCode.FailedPrecondition);
            incompatible = Hello();
            incompatible.LpsVersion = "0.0.0.0";
            await RejectAsync(token, incompatible, StatusCode.FailedPrecondition);
            using var validChannel = ClusterGrpcTransport.CreateChannel(address, token: token, certificateAuthorityPath: authority);
            var unauthorized = await Assert.ThrowsAsync<RpcException>(() => new NodeService.NodeServiceClient(validChannel)
                .CancelTestAsync(new CancelTestRequest(), deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync);
            Assert.Equal(StatusCode.Unauthenticated, unauthorized.StatusCode);
            var runSettingsPath = Assert.Single(Directory.GetFiles(directory, "run-settings.json", SearchOption.AllDirectories));
            using var runSettings = JsonDocument.Parse(await File.ReadAllTextAsync(runSettingsPath));
            using var runChannel = ClusterGrpcTransport.CreateChannel(address, token: runSettings.RootElement.GetProperty("Token").GetString(), certificateAuthorityPath: authority);
            var disabled = await Assert.ThrowsAsync<RpcException>(() => new NodeService.NodeServiceClient(runChannel)
                .CancelTestAsync(new CancelTestRequest(), deadline: DateTime.UtcNow.AddSeconds(5)).ResponseAsync);
            Assert.Equal(StatusCode.Unimplemented, disabled.StatusCode);
            using var valid = new WorkerControl.WorkerControlClient(validChannel).Connect(deadline: DateTime.UtcNow.AddSeconds(10));
            await valid.RequestStream.WriteAsync(Hello());
            Assert.True(await valid.ResponseStream.MoveNext(CancellationToken.None));
            Assert.Equal(WorkerAction.KeepAlive, valid.ResponseStream.Current.Action);
            await RejectAsync(token, Hello(endpoint: "http://127.0.0.1:51002"), StatusCode.AlreadyExists);
            await RejectAsync(token, Hello(name: "worker-2"), StatusCode.AlreadyExists);
            Assert.Empty(Directory.GetFiles(directory, "completion.json", SearchOption.AllDirectories));
        }
        finally { await StopAsync(master, directory); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("Category", "DistributedProcess")]
    public async Task PreparationBarrier_SendsNoLoad_WhenWorkerMissingOrCannotBind(bool occupiedPort, bool masterIsWorker)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-distributed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        output.WriteLine($"Retained artifacts: {directory}");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var masterAddress = $"http://127.0.0.1:{DistributedFiles.AvailablePort()}";
        using var target = new TcpListener(IPAddress.Loopback, 0);
        using var occupied = new TcpListener(IPAddress.Loopback, 0);
        target.Start();
        occupied.Start();
        var plan = Path.Combine(directory, "plan.json");
        await File.WriteAllTextAsync(plan, JsonSerializer.Serialize(new
        {
            Name = "Preparation barrier", Rounds = new[] { new
            {
                Name = "Round", NumberOfClients = "1", Iterations = new[] { new
                {
                    Name = "Request", RequestCount = "1",
                    HttpRequest = new { URL = $"http://127.0.0.1:{((IPEndPoint)target.LocalEndpoint).Port}/load", HttpMethod = "GET" }
                } }
            } }
        }));
        var source = Path.Combine(AppContext.BaseDirectory, "config", "lpsSettings.json");
        var processes = new List<(Process Process, Task<string> Output, Task<string> Error, string Name)>();
        try
        {
            processes.Add(Start("worker-1", directory, token, "worker", "--name", "worker-1", "--master", masterAddress,
                "--settings", source, "--data-directory", Path.Combine(directory, "worker-1")));
            if (occupiedPort)
                processes.Add(Start("worker-2", directory, token, "worker", "--name", "worker-2", "--master", masterAddress,
                    "--listen", $"http://127.0.0.1:{((IPEndPoint)occupied.LocalEndpoint).Port}", "--settings", source,
                    "--data-directory", Path.Combine(directory, "worker-2")));
            var master = Start("master", directory, token, "master", plan, "--listen", masterAddress, "--workers", "2", "--settings", source,
                "--masternodeisworker", masterIsWorker.ToString(),
                "--registration-timeout", occupiedPort ? "20" : "3", "--data-directory", Path.Combine(directory, "master"));
            processes.Add(master);
            await master.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            Assert.NotEqual(0, master.Process.ExitCode);
            Assert.False(target.Pending(), "Preparation sent traffic before all workers were ready.");
            var resultPath = Assert.Single(Directory.GetFiles(Path.Combine(directory, "master"), "cluster-result.json", SearchOption.AllDirectories));
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
            Assert.NotEqual("Completed", result.RootElement.GetProperty("State").GetString());
            foreach (var processPath in Directory.GetFiles(Path.Combine(directory, "master"), "process.json", SearchOption.AllDirectories))
            {
                using var processInfo = JsonDocument.Parse(await File.ReadAllTextAsync(processPath));
                Assert.Throws<ArgumentException>(() => Process.GetProcessById(processInfo.RootElement.GetProperty("ProcessId").GetInt32()));
            }
            Assert.All(processes.Where(process => process.Name.StartsWith("worker")), process => Assert.False(process.Process.HasExited));
        }
        finally { foreach (var process in processes) await StopAsync(process, directory); }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while (!predicate()) await timer.WaitForNextTickAsync(timeout.Token);
    }

    private static (Process Process, Task<string> Output, Task<string> Error, string Name) Start(string name, string directory, string token, params string[] arguments)
        => Start(name, directory, token, null, arguments);

    private static (Process Process, Task<string> Output, Task<string> Error, string Name) Start(string name, string directory, string token, string? sourceSettings, string[] arguments)
    {
        var sourceIndex = Array.IndexOf(arguments, "--settings");
        if (sourceIndex >= 0)
        {
            var copied = JsonNode.Parse(File.ReadAllText(arguments[sourceIndex + 1]))!;
            copied["LPSAppSettings"]!["Dashboard"]!["BuiltInDashboard"] = false;
            var settings = Path.Combine(directory, name + ".settings.json");
            File.WriteAllText(settings, copied.ToJsonString());
            arguments[sourceIndex + 1] = settings;
        }
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false, WorkingDirectory = directory, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(WorkerAgent).Assembly.Location);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["LPS_CLUSTER_TOKEN"] = token;
        start.Environment.Remove("LPS_CLUSTER_RUN_SETTINGS");
        start.Environment.Remove("LPS_WORKSPACE_RUN");
        if (sourceSettings != null) start.Environment["LPS_SETTINGS_FILE"] = sourceSettings;
        else start.Environment.Remove("LPS_SETTINGS_FILE");
        var process = Process.Start(start)!;
        return (process, process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync(), name);
    }

    private static string[] CreateCertificate(string directory)
    {
        using var authorityKey = RSA.Create(2048);
        var authorityRequest = new CertificateRequest("CN=LPS temporary process-test CA", authorityKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var authority = authorityRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        serverRequest.CertificateExtensions.Add(names.Build());
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var issued = serverRequest.Create(authority, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
        using var certificate = issued.CopyWithPrivateKey(serverKey);
        var certificatePath = Path.Combine(directory, "server.pfx");
        var authorityPath = Path.Combine(directory, "ca.pem");
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx));
        File.WriteAllText(authorityPath, authority.ExportCertificatePem());
        return ["--certificate", certificatePath, "--ca-cert", authorityPath];
    }
}