#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common;
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
    public sealed class CumulativeMetricsDispatcher : BackgroundService
    {
        private readonly ICumulativeMetricsQueue _queue;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly IInfluxDBWriter _influxDBWriter;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ICumulativeMetricsCoordinator _coordinator;
        private readonly int _refreshRateMs;
        private readonly ILogger<CumulativeMetricsDispatcher> _logger;

        public CumulativeMetricsDispatcher(
            ICumulativeMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            IInfluxDBWriter influxDBWriter,
            INodeMetadata nodeMetadata,
            ICumulativeMetricsCoordinator coordinator,
            IConfiguration configuration,
            ILogger<CumulativeMetricsDispatcher> logger)
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
            _logger.LogInformation("CumulativeMetricsDispatcher started, listening for snapshots.");

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
                await DrainQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CumulativeMetricsDispatcher encountered an error.");
                throw;
            }
            finally
            {
                await DrainQueueAsync();
            }
        }


        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            await DrainQueueAsync();
            _logger.LogInformation("CumulativeDispatcher stopped.");
            await base.StopAsync(stoppingToken);
        }
        private async Task PushSnapshotAsync(CumulativeIterationSnapshot snapshot , CancellationToken token)
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

        async ValueTask DrainQueueAsync()
        {
            // Skip drain logic for config commands - no metrics to drain
            if (!CommandContext.IsTestExecutionCommand)
            {
                _logger.LogDebug("CumulativeMetricsDispatcher: Skipping drain for config command.");
                return;
            }

            // Wait to ensure final data is in the queue - upon cancellation there is a very high probability of race condition so we have to wait
            await Task.Delay(_refreshRateMs);
            
            while (_queue.Reader.TryRead(out var snapshot))
            {
                _logger.LogDebug(
                    "CumulativeMetricsDispatcher: Draining snapshot for IterationId={IterationId}, IsFinal={IsFinal}, Status={ExecutionStatus}",
                    snapshot.IterationId, snapshot.IsFinal, snapshot.ExecutionStatus);
                await PushSnapshotAsync(snapshot, CancellationToken.None);
            }
        }

    }
}
