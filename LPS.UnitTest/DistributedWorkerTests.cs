using LPS.Common.Interfaces;
using LPS.Common.Services;
using LPS.Infrastructure.Distributed;
using LPS.UI.Core.Distributed;
using LPS.Protos.Shared;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json.Nodes;
using LPS.UI.Common.Options;
using LPS.Infrastructure.Nodes;
using Grpc.Core;
using NodeType = LPS.Infrastructure.Nodes.NodeType;
using Microsoft.Extensions.Configuration;
using LPS.Infrastructure.Grpc;
using System.CommandLine;
using LPS.UI.Core.LPSCommandLine;
using LPS.Infrastructure.GRPCClients.Factory;
using Google.Protobuf;

namespace LPS.UnitTest;

public class DistributedWorkerTests
{
    [Theory]
    [InlineData("http://127.0.0.1:51234", "http://127.0.0.1:51234")]
    [InlineData("https://worker.example:51234", "https://worker.example:51234")]
    [InlineData("http://[::1]:51234", "http://[::1]:51234")]
    [InlineData("127.0.0.1", "http://127.0.0.1:5001")]
    [InlineData("worker.example", "http://worker.example:5001")]
    [InlineData("::1", "http://[::1]:5001")]
    public void CallbackAddress_UsesAdvertisedEndpoint_WithLegacyHostFallback(string address, string expected)
    {
        var factory = new CustomGrpcClientFactory(new ClusterConfiguration("master.example", 5001, false, 1));
        var client = Mock.Of<IGRPCClient>();
        Assert.Same(client, factory.GetClient(address, resolved =>
        {
            Assert.Equal(expected, resolved);
            return client;
        }));
    }

    [Theory]
    [InlineData("http://127.0.0.1:0")]
    [InlineData("http://worker.example:51234/path")]
    [InlineData("http://user@worker.example:51234")]
    [InlineData("http://worker.example:51234?query=value")]
    [InlineData("http://worker.example:51234#fragment")]
    [InlineData("ftp://worker.example:51234")]
    [InlineData("not-an-endpoint")]
    public async Task NodeRegistration_RejectsInvalidCallbackBeforeRegistering(string endpoint)
    {
        var registry = new Mock<INodeRegistry>();
        using var cancellation = new CancellationTokenSource();
        var service = new global::Apis.Services.NodeGRPCService(registry.Object, Mock.Of<IClusterConfiguration>(),
            Mock.Of<ITestTriggerNotifier>(), Mock.Of<LPS.Domain.Common.Interfaces.ILogger>(),
            Mock.Of<LPS.Domain.Common.Interfaces.IRuntimeOperationIdProvider>(), Mock.Of<ICustomGrpcClientFactory>(), cancellation);
        var error = await Assert.ThrowsAsync<RpcException>(() => service.RegisterNode(new LPS.Protos.Shared.NodeMetadata
        {
            NodeName = "worker", NodeIp = "127.0.0.1", Endpoint = endpoint
        }, null!));
        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
        registry.Verify(registry => registry.RegisterNode(It.IsAny<INode>()), Times.Never);
    }

    [Theory]
    [InlineData("--masternodeport")]
    [InlineData("--grpcport")]
    [InlineData("-gp")]
    public void MasterNodePort_CommandAcceptsNewAndLegacyNames(string option)
    {
        var command = new RootCommand();
        command.AddOption(CommandLineOptions.LPSClusterCommandOptions.GRPCPortOption);
        var parsed = command.Parse([option, "51234"]);
        Assert.Empty(parsed.Errors);
        Assert.Equal(51234, parsed.GetValueForOption(CommandLineOptions.LPSClusterCommandOptions.GRPCPortOption));
    }

    [Theory]
    [InlineData(null, "http://127.0.0.1:5001", "192.168.1.6", "http://127.0.0.1:0")]
    [InlineData(null, "https://master.example:5001", "192.168.1.6", "https://192.168.1.6:0")]
    [InlineData("https://worker.example:51234", "https://master.example:5001", "192.168.1.6", "https://worker.example:51234")]
    public void WorkerEndpoint_DoesNotInheritMasterPort(string? address, string master, string local, string expected)
    {
        Assert.Equal(expected, DistributedCommand.ResolveWorkerEndpoint(address, master, local));
    }

    [Theory]
    [InlineData("http://127.0.0.1:51234")]
    [InlineData(null)]
    public void NodeRegistration_PreservesEndpointAndRole_WithoutChangingIdentity(string? endpoint)
    {
        var cluster = new ClusterConfiguration("127.0.0.1", 5001, false, 1);
        var metadata = new LPS.Infrastructure.Nodes.NodeMetadata(cluster, "worker", "127.0.0.1", "", "", "", "", 0, "", [], [],
            endpoint, NodeType.Worker);
        var restored = LPS.Protos.Shared.NodeMetadata.Parser.ParseFrom(metadata.ToProto().ToByteArray()).FromProto(cluster);
        Assert.Equal(endpoint, restored.Endpoint);
        Assert.Equal("127.0.0.1", restored.NodeIP);
        Assert.Equal(NodeType.Worker, restored.NodeType);
    }

    [Theory]
    [InlineData("{\"GRPCPort\":5001}", 5001)]
    [InlineData("{\"MasterNodePort\":5002}", 5002)]
    [InlineData("{\"GRPCPort\":5001,\"MasterNodePort\":5002}", 5002)]
    [InlineData("{\"MasterNodePort\":5002,\"GRPCPort\":5001}", 5002)]
    public void MasterNodePort_AcceptsLegacyAlias_WithCanonicalPrecedence(string json, int expected)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using var configuration = new ConfigurationManager();
        configuration.AddJsonStream(stream);
        Assert.Equal(expected, configuration.Get<ClusterConfigurationOptions>()!.MasterNodePort);
        Assert.Equal(expected, System.Text.Json.JsonSerializer.Deserialize<ClusterConfigurationOptions>(json)!.MasterNodePort);
        Assert.Equal(expected, Newtonsoft.Json.JsonConvert.DeserializeObject<ClusterConfigurationOptions>(json)!.MasterNodePort);
        var cluster = new ClusterConfigurationOptions { MasterNodeIP = "127.0.0.1", MasterNodePort = expected, GRPCPort = 1 };
        Assert.Equal($"http://127.0.0.1:{expected}", DistributedCommand.ResolveMasterEndpoint(null, cluster));
    }

    [Fact]
    public async Task MasterParticipation_ReservesLocalSlotBeforeTheLocalWorkerRegisters()
    {
        using var cancellation = new CancellationTokenSource();
        var settings = new ClusterRunSettings
        {
            RunId = Guid.NewGuid().ToString(), NodeName = "master", Role = NodeType.Master,
            RunDirectory = "unused", PlanPath = "unused", NodeAddress = "http://127.0.0.1:5101",
            MasterAddress = "http://127.0.0.1:5101", Token = new string('x', 64),
            ExpectedWorkers = 2, MasterNodeIsWorker = true
        };
        var coordinator = new MasterCoordinator(settings, Mock.Of<INodeRegistry>(), Mock.Of<IClusterConfiguration>());
        Task ConnectAsync(string name, string endpoint)
        {
            var reader = new Mock<IAsyncStreamReader<WorkerUpdate>>();
            reader.SetupGet(stream => stream.Current).Returns(new WorkerUpdate
            {
                WorkerId = name, Endpoint = endpoint, SessionId = Guid.NewGuid().ToString(), State = WorkerRunState.Idle,
                ProtocolVersion = MasterCoordinator.ProtocolVersion, LpsVersion = MasterCoordinator.Version
            });
            var initial = true;
            reader.Setup(stream => stream.MoveNext(It.IsAny<CancellationToken>())).Returns(async (CancellationToken token) =>
            {
                if (initial) { initial = false; return true; }
                await Task.Delay(Timeout.Infinite, token);
                return false;
            });
            var writer = new Mock<IServerStreamWriter<WorkerInstruction>>();
            writer.Setup(stream => stream.WriteAsync(It.IsAny<WorkerInstruction>())).Returns(Task.CompletedTask);
            return coordinator.ConnectAsync(reader.Object, writer.Object, cancellation.Token);
        }

        var external = ConnectAsync("worker-1", "http://127.0.0.1:5102");
        var local = Task.CompletedTask;
        try
        {
            Assert.False(external.IsCompleted);
            var rejected = await Assert.ThrowsAsync<RpcException>(() => ConnectAsync("extra-worker", "http://127.0.0.1:5103"));
            Assert.Equal(StatusCode.ResourceExhausted, rejected.StatusCode);
            local = ConnectAsync(MasterCoordinator.LocalWorkerName, "http://127.0.0.1:5104");
            Assert.False(local.IsCompleted);
        }
        finally
        {
            cancellation.Cancel();
            try { await Task.WhenAll(external, local); }
            catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData(null, "127.0.0.1", 5161, "http://127.0.0.1:5161")]
    [InlineData(null, "::1", 5161, "http://[::1]:5161")]
    [InlineData(null, "master.example", 5161, "https://master.example:5161")]
    [InlineData(null, "https://master.example:5165", 5161, "https://master.example:5165")]
    [InlineData("http://127.0.0.1:5168", "master.example", 5161, "http://127.0.0.1:5168")]
    [InlineData(null, null, null, null)]
    public void MasterEndpoint_UsesLegacySettings_WithCliPrecedence(string? address, string? host, int? port, string? expected)
    {
        var cluster = new ClusterConfigurationOptions { MasterNodeIP = host, GRPCPort = port };
        Assert.Equal(expected, DistributedCommand.ResolveMasterEndpoint(address, cluster));
    }

    [Theory]
    [InlineData("http://master.example:5161", 5161)]
    [InlineData("https://master.example:5161/path", 5161)]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.1", null)]
    public void MasterEndpoint_RejectsUnsafeOrIncompleteSettings(string host, int? port)
    {
        Assert.Throws<ArgumentException>(() => DistributedCommand.ResolveMasterEndpoint(null,
            new ClusterConfigurationOptions { MasterNodeIP = host, GRPCPort = port }));
    }

    [Theory]
    [InlineData(NodeType.Master, true, true)]
    [InlineData(NodeType.Master, false, false)]
    [InlineData(NodeType.Worker, true, false)]
    public async Task RunSettings_PreserveMasterDashboardPreference_AndKeepWorkersHeadless(NodeType role, bool configured, bool expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-dashboard-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.json");
        var document = new JsonObject { ["LPSAppSettings"] = new JsonObject
        {
            ["Dashboard"] = new JsonObject { ["BuiltInDashboard"] = configured, ["Port"] = 8009 }
        } };
        var original = document.ToJsonString();
        await File.WriteAllTextAsync(source, original);
        var run = new ClusterRunSettings
        {
            RunId = Guid.NewGuid().ToString(), NodeName = "test", Role = role,
            RunDirectory = Path.Combine(directory, "run"), PlanPath = Path.Combine(directory, "run", "plan.json"),
            NodeAddress = "http://127.0.0.1:5102", MasterAddress = "http://127.0.0.1:5101", Token = new string('x', 64)
        };
        try
        {
            var path = await DistributedFiles.WriteSettingsAsync(run, source, 8110, CancellationToken.None);
            var copied = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            Assert.Equal(expected, copied["LPSAppSettings"]!["Dashboard"]!["BuiltInDashboard"]!.GetValue<bool>());
            Assert.Equal(8110, copied["LPSAppSettings"]!["Dashboard"]!["Port"]!.GetValue<int>());
            Assert.Equal(original, await File.ReadAllTextAsync(source));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task PrivateRunFiles_RestrictAccessToCurrentUser()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-permissions-test", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(directory, "credentials.json");
        try
        {
            await DistributedFiles.WritePrivateAsync(file, "{}");
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var security = new DirectoryInfo(directory).GetAccessControl();
                Assert.True(security.AreAccessRulesProtected);
                var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>();
                Assert.All(rules, rule => Assert.Equal(identity.User, rule.IdentityReference));
            }
            else Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("json")]
    [InlineData("master")]
    [InlineData("replay")]
    public async Task Assignment_RejectsUnsafeOrReplayedPlans_BeforeSpawning(string invalid)
    {
        var directory = Path.Combine(Path.GetTempPath(), "lps-assignment-test", Guid.NewGuid().ToString("N"));
        var command = new WorkerInstruction
        {
            RunId = Guid.NewGuid().ToString(), MasterEndpoint = "http://127.0.0.1:5001", RunToken = new string('x', 64),
            StartedUtc = DateTime.UtcNow.ToString("O"), PlanJson = invalid == "json" ? "{" : invalid == "size" ? new string(' ', 1024 * 1024 + 1) : "{}"
        };
        command.PlanHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command.PlanJson)));
        if (invalid == "hash") command.PlanHash = "incorrect";
        if (invalid == "master") command.MasterEndpoint = "http://127.0.0.1:5002";
        var agent = new WorkerAgent(new WorkerAgentOptions
        {
            Name = "worker-1", DataDirectory = directory, MasterAddress = "http://127.0.0.1:5001",
            ListenAddress = "http://127.0.0.1:5003", SettingsPath = "unused", Token = "unused"
        });
        try
        {
            if (invalid == "replay")
            {
                var claimed = Path.Combine(directory, "runs", Guid.Parse(command.RunId).ToString("N"));
                Directory.CreateDirectory(claimed);
                await File.WriteAllTextAsync(Path.Combine(claimed, "claimed"), "");
            }
            Task PrepareAsync() => agent.PrepareAsync(command, "http://127.0.0.1:5003", CancellationToken.None);
            if (invalid == "json") await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(PrepareAsync);
            else if (invalid == "replay") await Assert.ThrowsAsync<IOException>(PrepareAsync);
            else await Assert.ThrowsAsync<InvalidOperationException>(PrepareAsync);
            if (Directory.Exists(directory)) Assert.Empty(Directory.GetFiles(directory, "process.json", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Session_RequiresReadinessAndStartAuthorization()
    {
        var session = new WorkerSession("worker-1", "session", "run");
        session.Assign();
        WorkerUpdate Update(WorkerRunState state) => new() { WorkerId = "worker-1", SessionId = "session", RunId = "run", State = state, Endpoint = "http://127.0.0.1:5102" };
        Assert.Throws<InvalidOperationException>(() => session.Apply(Update(WorkerRunState.Running)));
        Assert.True(session.Apply(Update(WorkerRunState.Ready)));
        Assert.False(session.Ready.Task.IsCompleted);
        session.PublishReady();
        Assert.Equal(WorkerRunState.Ready, session.Ready.Task.Result);
        Assert.False(session.Apply(Update(WorkerRunState.Ready)));
        Assert.Throws<InvalidOperationException>(() => session.Apply(Update(WorkerRunState.Running)));
        session.AuthorizeStart();
        Assert.True(session.Apply(Update(WorkerRunState.Running)));
        Assert.Throws<InvalidOperationException>(() => session.Apply(Update(WorkerRunState.Completed)));
        Assert.True(session.Apply(Update(WorkerRunState.Finalizing)));
        Assert.True(session.Apply(Update(WorkerRunState.Completed)));
        session.Disconnected();
        Assert.Equal(WorkerRunState.Completed, session.State);
    }

    [Fact]
    public void Session_RejectsStaleUpdatesAndMarksDisconnectionsFailed()
    {
        var session = new WorkerSession("worker-1", "new-session", "new-run");
        session.Assign();
        Assert.Throws<InvalidOperationException>(() => session.Apply(new WorkerUpdate
        {
            WorkerId = "worker-1", SessionId = "old-session", RunId = "old-run", State = WorkerRunState.Cancelled
        }));
        Assert.Equal(WorkerRunState.Preparing, session.State);
        session.Disconnected();
        Assert.Equal(WorkerRunState.Failed, session.Ready.Task.Result);
        Assert.Equal(WorkerRunState.Failed, session.Finished.Task.Result);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5101")]
    [InlineData("http://[::1]:5102")]
    [InlineData("https://worker.example:5103")]
    public void Endpoints_AcceptLoopbackOrTls(string endpoint)
    {
        Assert.Equal(endpoint, ClusterRunSettings.ValidateEndpoint(endpoint).GetLeftPart(UriPartial.Authority));
    }

    [Theory]
    [InlineData("http://192.168.1.50:5001")]
    [InlineData("https://user:password@worker:5001")]
    [InlineData("https://worker:5001/plan")]
    [InlineData("https://worker:5001?token=secret")]
    [InlineData("file:///tmp/plan")]
    public void Endpoints_RejectUnsafeAddresses(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => ClusterRunSettings.ValidateEndpoint(endpoint));
    }

    [Fact]
    public async Task Trigger_DeliversEachObserverOnceAndAwaitsAcknowledgements()
    {
        var notifier = new TestTriggerNotifier();
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new Mock<ITestTriggerObserver>();
        var second = new Mock<ITestTriggerObserver>();
        first.Setup(observer => observer.OnTestTriggered()).Returns(acknowledged.Task);
        second.Setup(observer => observer.OnTestTriggered()).Returns(Task.CompletedTask);
        notifier.RegisterObserver(first.Object);
        notifier.RegisterObserver(first.Object);
        notifier.RegisterObserver(second.Object);

        var notification = notifier.NotifyObserversAsync();
        Assert.False(notification.IsCompleted);
        second.Verify(observer => observer.OnTestTriggered(), Times.Once);
        await notifier.NotifyObserversAsync();
        acknowledged.SetResult();
        await notification;

        first.Verify(observer => observer.OnTestTriggered(), Times.Once);
        second.Verify(observer => observer.OnTestTriggered(), Times.Once);
    }

    [Fact]
    public async Task Trigger_ObservesFailuresAndDoesNotReplayTheSignal()
    {
        var notifier = new TestTriggerNotifier();
        var observer = new Mock<ITestTriggerObserver>();
        observer.Setup(value => value.OnTestTriggered()).Returns(Task.FromException(new InvalidOperationException("rejected")));
        notifier.RegisterObserver(observer.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(notifier.NotifyObserversAsync);
        await notifier.NotifyObserversAsync();

        observer.Verify(value => value.OnTestTriggered(), Times.Once);
    }
}