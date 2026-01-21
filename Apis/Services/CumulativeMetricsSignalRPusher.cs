#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Apis.Services
{
    /// <summary>
    /// Background service that reads cumulative metric snapshots from the queue
    /// and pushes them to connected SignalR clients and customer's InfluxDB.
    /// Cumulative data is pushed at its own interval (RefreshRate), separate from windowed data.
    /// Only master node uploads to InfluxDB (workers have partial data).
    /// </summary>
    public sealed class CumulativeMetricsSignalRPusher : BackgroundService
    {
        private readonly ICumulativeMetricsQueue _queue;
        private readonly IHubContext<WindowedMetricsHub> _hubContext;
        private readonly IInfluxDBWriter _influxDBWriter;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ICumulativeMetricsCoordinator _coordinator;
        private readonly int _refreshRateMs;
        private readonly ILogger<CumulativeMetricsSignalRPusher> _logger;

        public CumulativeMetricsSignalRPusher(
            ICumulativeMetricsQueue queue,
            IHubContext<WindowedMetricsHub> hubContext,
            IInfluxDBWriter influxDBWriter,
            INodeMetadata nodeMetadata,
            ICumulativeMetricsCoordinator coordinator,
            IConfiguration configuration,
            ILogger<CumulativeMetricsSignalRPusher> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _influxDBWriter = influxDBWriter ?? throw new ArgumentNullException(nameof(influxDBWriter));
            _nodeMetadata = nodeMetadata ?? throw new ArgumentNullException(nameof(nodeMetadata));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            var refreshRate = configuration.GetValue<int?>("LPSAppSettings:Dashboard:RefreshRate") ?? 3;
            _refreshRateMs = refreshRate * 1000;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CumulativeMetricsSignalRPusher started, listening for snapshots.");

            try
            {
                // Wait for snapshots and push to SignalR
                await foreach (var snapshot in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    await PushSnapshotAsync(snapshot, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CumulativeMetricsSignalRPusher encountered an error.");
                throw;
            }
            finally
            {
                _logger.LogInformation("CumulativeMetricsSignalRPusher stopping, waiting for coordinator to finish...");

                // Keep draining while the coordinator is still running
                // The coordinator will push final snapshots before stopping
                // MUST drain here in ExecuteAsync - SignalR is still alive during this window
                // If we move this to StopAsync, SignalR will already be disposed
                while (_coordinator.IsRunning)
                {
                    // Wait a bit before checking again
                    await Task.Delay(_refreshRateMs);
                }

                while (_queue.Reader.TryRead(out var snapshot))
                {
                    _logger.LogDebug(
                        "CumulativeMetricsSignalRPusher: Draining snapshot for IterationId={IterationId}, IsFinal={IsFinal}, Status={ExecutionStatus}",
                        snapshot.IterationId, snapshot.IsFinal, snapshot.ExecutionStatus);
                    await PushSnapshotAsync(snapshot, CancellationToken.None);
                }
                _logger.LogInformation("CumulativeMetricsSignalRPusher finished draining queue.");

                _logger.LogInformation("CumulativeMetricsSignalRPusher stopped.");
            }
        }


        private async Task PushSnapshotAsync(CumulativeIterationSnapshot snapshot, CancellationToken token)
        {
            try
            {
                var iterationGroup = snapshot.IterationId.ToString();

                // Send to specific iteration subscribers
                await _hubContext.Clients
                    .Group(iterationGroup)
                    .SendAsync("ReceiveCumulativeMetrics", snapshot, token);

                // Also broadcast to "all" subscribers
                await _hubContext.Clients
                    .Group("all")
                    .SendAsync("ReceiveCumulativeMetrics", snapshot, token);

                _logger.LogDebug(
                    "Pushed cumulative snapshot for {IterationName} (final: {IsFinal})",
                    snapshot.IterationName,
                    snapshot.IsFinal);

                // Upload to customer's InfluxDB
                // Only master uploads - workers have partial data, master has aggregated metrics
                if (_nodeMetadata.NodeType == NodeType.Master)
                {
                    await _influxDBWriter.UploadCumulativeMetricsAsync(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to push cumulative snapshot for {IterationId}",
                    snapshot.IterationId);
            }
        }
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            // Draining is already done in ExecuteAsync catch block (while SignalR is still alive)
            // By this point, SignalR may already be disposed
            _logger.LogInformation("CumulativeMetricsSignalRPusher StopAsync called.");
            await base.StopAsync(stoppingToken);
        }

    }
}
