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
    public async Task WatchdogReportsKeepMachinesSeparateWithoutIterationDiscovery()
    {
        var tracker = new CoolingTracker();
        var service = new CoolingGrpcService(tracker, Metadata(NodeType.Master, "master"));
        var request = new CoolingReportRequest();
        request.Updates.Add(Update("worker-1"));
        request.Updates.Add(Update("worker-2"));

        Assert.True((await service.Report(request, null!)).Success);
        Assert.True((await service.Report(request, null!)).Success);

        var periods = tracker.GetPeriods("example.com", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
        Assert.Equal(2, periods.Count);
        Assert.Equal(new[] { "worker-1", "worker-2" }, periods.Select(period => period.NodeId));
        Assert.All(periods, period => Assert.Equal("CPU above limit", period.Reason));
    }

    [Fact]
    public async Task UnsupportedOrInvalidReportsDoNotPartiallyApply()
    {
        var tracker = new Mock<ICoolingTracker>();
        var service = new CoolingGrpcService(tracker.Object, Metadata(NodeType.Master, "master"));
        var request = new CoolingReportRequest();
        request.Updates.Add(Update("worker-1"));
        var unsupported = Update("worker-1");
        unsupported.Source = "Unsupported";
        request.Updates.Add(unsupported);

        var unknown = await Assert.ThrowsAsync<RpcException>(() => service.Report(request, null!));
        Assert.Equal(StatusCode.InvalidArgument, unknown.StatusCode);
        tracker.Verify(value => value.ApplyUpdate(It.IsAny<CoolingUpdate>()), Times.Never);

        request.Updates[1].Source = "Watchdog";
        request.Updates[1].End = null;
        var invalid = await Assert.ThrowsAsync<RpcException>(() => service.Report(request, null!));
        Assert.Equal(StatusCode.InvalidArgument, invalid.StatusCode);
        tracker.Verify(value => value.ApplyUpdate(It.IsAny<CoolingUpdate>()), Times.Never);
    }

    [Fact]
    public async Task WorkerReporterSendsActiveAndFinalIntervalsOverGrpc()
    {
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
        builder.Services.AddSingleton(Metadata(NodeType.Master, "master"));
        await using var app = builder.Build();
        app.MapGrpcService<CoolingGrpcService>();
        await app.StartAsync();
        try
        {
            var address = new Uri(app.Urls.Single());
            var cluster = Mock.Of<IClusterConfiguration>(value => value.MasterNodeIP == "127.0.0.1" && value.MasterNodePort == address.Port);
            var workerTracker = new CoolingTracker(nodeId: "worker-1", machineName: "worker-east");
            using var reporter = new CoolingMetricsReporter(workerTracker, Metadata(NodeType.Worker, "worker-1"), cluster,
                new CustomGrpcClientFactory(cluster), NullLogger<CoolingMetricsReporter>.Instance);
            var start = DateTime.UtcNow;
            workerTracker.SetWatchdogState("example.com", ResourceState.Hot, "Short CPU pressure");
            workerTracker.SetWatchdogState("example.com", ResourceState.Cool);
            await reporter.FlushAsync();
            receiver.Verify(value => value.ApplyUpdate(It.Is<CoolingUpdate>(update =>
                !update.IsActive && update.Period.Reason == "Short CPU pressure")), Times.Once);
            workerTracker.SetWatchdogState("example.com", ResourceState.Hot, "CPU at/above 70% limit");
            await reporter.StartAsync(CancellationToken.None);
            await receivedActive.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await reporter.CompleteAsync();
            await reporter.CompleteAsync();

            receiver.Verify(value => value.ApplyUpdate(It.Is<CoolingUpdate>(update => !update.IsActive && update.Period.Source == "Watchdog")), Times.AtLeastOnce);
            receiver.Verify(value => value.ApplyUpdate(It.Is<CoolingUpdate>(update => !update.IsActive &&
                update.Period.Reason == "CPU at/above 70% limit")), Times.AtLeastOnce);
            var periods = masterTracker.GetPeriods("example.com", start, DateTime.UtcNow);
            Assert.Contains(periods, period => period.Reason == "Short CPU pressure");
            Assert.Contains(periods, period => period.Reason == "CPU at/above 70% limit");
            Assert.All(periods, period =>
            {
                Assert.Equal("worker-1", period.NodeId);
                Assert.Equal("worker-east", period.MachineName);
                Assert.NotEmpty(period.Reason);
                Assert.Equal("Watchdog", period.Source);
            });
        }
        finally { await app.StopAsync(); }
    }

    private static INodeMetadata Metadata(NodeType type, string nodeId) =>
        Mock.Of<INodeMetadata>(value => value.NodeType == type && value.NodeIP == nodeId && value.NodeName == nodeId);

    private static CoolingStateUpdate Update(string node)
    {
        var end = DateTime.UtcNow.AddSeconds(-1);
        return new CoolingStateUpdate
        {
            NodeId = node, MachineName = node, HostName = "example.com",
            Source = "Watchdog", Reason = "CPU above limit", Start = Timestamp.FromDateTime(end.AddSeconds(-2)),
            End = Timestamp.FromDateTime(end)
        };
    }
}