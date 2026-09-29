using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodeType = LPS.Infrastructure.Nodes.NodeType;

namespace LPS.Infrastructure.Monitoring;

public sealed class CoolingMetricsReporter : BackgroundService
{
    private readonly ICoolingTracker _tracker;
    private readonly GrpcCoolingClient _client;
    private readonly ILogger<CoolingMetricsReporter> _logger;
    private DateTime _sentThrough = DateTime.MinValue;
    private Task _completion;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    public CoolingMetricsReporter(ICoolingTracker tracker, INodeMetadata node, IClusterConfiguration cluster,
        ICustomGrpcClientFactory clients, ILogger<CoolingMetricsReporter> logger)
    {
        _tracker = tracker;
        _logger = logger;
        if (node.NodeType != NodeType.Master)
            _client = clients.GetClient<GrpcCoolingClient>(cluster.MasterNodeIP);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_client == null) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task FlushAsync(CancellationToken token = default)
    {
        if (_client == null) return;
        await _sendGate.WaitAsync(token);
        try { await SendAsync(token); }
        finally { _sendGate.Release(); }
    }

    private async Task SendAsync(CancellationToken token)
    {
        var until = DateTime.UtcNow;
        var updates = _tracker.GetUpdates(_sentThrough, until);
        if (updates.Count == 0) return;
        var request = new CoolingReportRequest();
        foreach (var update in updates)
            request.Updates.Add(new CoolingStateUpdate
            {
                IterationId = update.IterationId.ToString(), HostName = update.HostName,
                Source = update.Period.Source, Start = Timestamp.FromDateTime(update.Period.Start),
                End = Timestamp.FromDateTime(update.Period.End), NodeId = update.Period.NodeId,
                MachineName = update.Period.MachineName, Reason = update.Period.Reason, IsActive = update.IsActive
            });
        try
        {
            var response = await _client.ReportAsync(request, deadline: DateTime.UtcNow.AddSeconds(3), cancellationToken: token);
            if (response.Success) _sentThrough = until;
        }
        catch (RpcException exception) when (!token.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not forward cooling intervals to the master.");
        }
        catch (RpcException) when (token.IsCancellationRequested) { }
    }

    public Task CompleteAsync() => _completion ??= CompleteCoreAsync();

    private async Task CompleteCoreAsync()
    {
        await base.StopAsync(CancellationToken.None);
        _tracker.Complete();
        await FlushAsync();
    }

    public override Task StopAsync(CancellationToken cancellationToken) => CompleteAsync().WaitAsync(cancellationToken);
}