using System.Net;
using Apis.Services;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Monitoring;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodeType = LPS.Infrastructure.Nodes.NodeType;

namespace LPS.UnitTest;

public sealed class CoolingGrpcServiceTests
{
    [Fact]
    public async Task ReportsTranslateWorkerIterationsAndKeepMachinesSeparate()
    {
        var workerIteration = Guid.NewGuid();
        var masterIteration = Guid.NewGuid();
        var tracker = new CoolingTracker();
        var service = new CoolingGrpcService(tracker, Discovery(workerIteration, masterIteration), Metadata(NodeType.Master, "master"));
        var request = new CoolingReportRequest();
        request.Updates.Add(Update(workerIteration, "worker-1", "BatchCooldown"));
        request.Updates.Add(Update(workerIteration, "worker-2", "BatchCooldown"));

        Assert.True((await service.Report(request, null!)).Success);
        Assert.True((await service.Report(request, null!)).Success);

        var periods = tracker.GetPeriods("example.com", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow, masterIteration);
        Assert.Equal(2, periods.Count);
        Assert.Equal(new[] { "worker-1", "worker-2" }, periods.Select(period => period.NodeId));
        Assert.All(periods, period => Assert.Equal("CRB batch cooldown: 250 ms", period.Reason));
        Assert.Empty(tracker.GetPeriods("example.com", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow, workerIteration));
    }

    [Fact]
    public async Task InvalidOrUndiscoveredReportsDoNotPartiallyApply()
    {
        var tracker = new Mock<ICoolingTracker>();
        var service = new CoolingGrpcService(tracker.Object, Mock.Of<IEntityDiscoveryService>(), Metadata(NodeType.Master, "master"));
        var request = new CoolingReportRequest();
        request.Updates.Add(Update(Guid.Empty, "worker-1", "Watchdog"));
        request.Updates.Add(Update(Guid.NewGuid(), "worker-1", "BatchCooldown"));

        var unknown = await Assert.ThrowsAsync<RpcException>(() => service.Report(request, null!));
        Assert.Equal(StatusCode.FailedPrecondition, unknown.StatusCode);
        tracker.Verify(value => value.ApplyUpdate(It.IsAny<CoolingUpdate>()), Times.Never);

        request.Updates[1].Source = "invalid";
        var invalid = await Assert.ThrowsAsync<RpcException>(() => service.Report(request, null!));
        Assert.Equal(StatusCode.InvalidArgument, invalid.StatusCode);
    }

    [Fact]
    public async Task WorkerReporterSendsActiveAndFinalIntervalsOverGrpc()
    {
        var workerIteration = Guid.NewGuid();
        var masterIteration = Guid.NewGuid();
        var masterTracker = new CoolingTracker();
        var receivedActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var receiver = new Mock<ICoolingTracker>();
        receiver.Setup(value => value.ApplyUpdate(It.IsAny<CoolingUpdate>())).Callback<CoolingUpdate>(update =>
        {
            masterTracker.ApplyUpdate(update);
            if (update.IsActive) receivedActive.TrySetResult();
        });
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(receiver.Object);
        builder.Services.AddSingleton(Discovery(workerIteration, masterIteration));
        builder.Services.AddSingleton(Metadata(NodeType.Master, "master"));
        await using var app = builder.Build();
        app.MapGrpcService<CoolingGrpcService>();
        await app.StartAsync();
        try
        {
            var address = new Uri(app.Urls.Single());
            var cluster = Mock.Of<IClusterConfiguration>(value => value.MasterNodeIP == "127.0.0.1" && value.GRPCPort == address.Port);
            var workerTracker = new CoolingTracker(nodeId: "worker-1", machineName: "worker-east");
            using var reporter = new CoolingMetricsReporter(workerTracker, Metadata(NodeType.Worker, "worker-1"), cluster,
                new CustomGrpcClientFactory(cluster), NullLogger<CoolingMetricsReporter>.Instance);
            var start = DateTime.UtcNow;
            using (workerTracker.BeginBatchCooldown(workerIteration, "example.com", "Short batch pause")) { }
            await reporter.FlushAsync();
            receiver.Verify(value => value.ApplyUpdate(It.Is<CoolingUpdate>(update =>
                !update.IsActive && update.IterationId == masterIteration && update.Period.Reason == "Short batch pause")), Times.Once);
            using var scope = workerTracker.BeginBatchCooldown(workerIteration, "example.com", "CRB batch cooldown: 250 ms");
            workerTracker.SetWatchdogState("example.com", ResourceState.Hot, "CPU at/above 70% limit");
            await reporter.StartAsync(CancellationToken.None);
            await receivedActive.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await reporter.CompleteAsync();
            await reporter.CompleteAsync();

            receiver.Verify(value => value.ApplyUpdate(It.Is<CoolingUpdate>(update => !update.IsActive && update.Period.Source == "Watchdog")), Times.AtLeastOnce);
            receiver.Verify(value => value.ApplyUpdate(It.Is<CoolingUpdate>(update => !update.IsActive && update.IterationId == masterIteration)), Times.AtLeastOnce);
            var periods = masterTracker.GetPeriods("example.com", start, DateTime.UtcNow, masterIteration);
            Assert.Contains(periods, period => period.Reason == "CRB batch cooldown: 250 ms");
            Assert.Contains(periods, period => period.Source == "Watchdog");
            Assert.All(periods, period =>
            {
                Assert.Equal("worker-1", period.NodeId);
                Assert.Equal("worker-east", period.MachineName);
                Assert.NotEmpty(period.Reason);
            });
        }
        finally { await app.StopAsync(); }
    }

    private static INodeMetadata Metadata(NodeType type, string nodeId) =>
        Mock.Of<INodeMetadata>(value => value.NodeType == type && value.NodeIP == nodeId && value.NodeName == nodeId);

    private static IEntityDiscoveryService Discovery(Guid workerIteration, Guid masterIteration)
    {
        var records = new List<IEntityDiscoveryRecord>();
        foreach (var nodeId in new[] { "worker-1", "worker-2", "master" })
        {
            var master = nodeId == "master";
            var node = Mock.Of<INode>(value => value.Metadata == Metadata(master ? NodeType.Master : NodeType.Worker, nodeId));
            var iteration = master ? masterIteration : workerIteration;
            records.Add(Mock.Of<IEntityDiscoveryRecord>(value => value.IterationId == iteration && value.FullyQualifiedName == "Plan.Round.Request" && value.Node == node));
        }
        var discovery = new Mock<IEntityDiscoveryService>();
        discovery.Setup(value => value.Discover(It.IsAny<Func<IEntityDiscoveryRecord, bool>>()))
            .Returns<Func<IEntityDiscoveryRecord, bool>>(predicate => records.Where(predicate).ToList());
        return discovery.Object;
    }

    private static CoolingStateUpdate Update(Guid iteration, string node, string source)
    {
        var end = DateTime.UtcNow.AddSeconds(-1);
        return new CoolingStateUpdate
        {
            IterationId = iteration.ToString(), NodeId = node, MachineName = node, HostName = "example.com",
            Source = source, Reason = "CRB batch cooldown: 250 ms", Start = Timestamp.FromDateTime(end.AddSeconds(-2)),
            End = Timestamp.FromDateTime(end)
        };
    }
}