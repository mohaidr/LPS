#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.Infrastructure.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apis.Services
{
    /// <summary>
    /// Background service that reads windowed metric snapshots from the queue
    /// and pushes them to connected SignalR clients and customer's InfluxDB.
    /// Clean separation: just reads and forwards, no knowledge of window timing.
    /// Only master node uploads to InfluxDB (workers have partial data).
    /// </summary>
    public sealed class WindowedMetricsSignalRPusher : BackgroundService
    {
        private readonly IWindowedMetricsQueue _queue;
        private readonly IHubContext<WindowedMetricsHub> _hubContext;
        private readonly IInfluxDBWriter _influxDBWriter;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ILogger<WindowedMetricsSignalRPusher> _logger;

        public WindowedMetricsSignalRPusher(
            IWindowedMetricsQueue queue,
            IHubContext<WindowedMetricsHub> hubContext,
            IInfluxDBWriter influxDBWriter,
            INodeMetadata nodeMetadata,
            ILogger<WindowedMetricsSignalRPusher> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _influxDBWriter = influxDBWriter ?? throw new ArgumentNullException(nameof(influxDBWriter));
            _nodeMetadata = nodeMetadata ?? throw new ArgumentNullException(nameof(nodeMetadata));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WindowedMetricsSignalRPusher started, listening for snapshots.");

            try
            {
                // Wait for snapshots and push to SignalR
                await foreach (var snapshot in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    await PushSnapshotAsync(snapshot, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("WindowedMetricsSignalRPusher stopping due to cancellation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WindowedMetricsSignalRPusher encountered an error.");
                throw;
            }
        }

        private async Task PushSnapshotAsync(WindowedIterationSnapshot snapshot, CancellationToken token)
        {
            try
            {
                var iterationGroup = snapshot.IterationId.ToString();

                // Send to specific iteration subscribers using the method name the frontend expects
                await _hubContext.Clients
                    .Group(iterationGroup)
                    .SendAsync("ReceiveWindowedMetrics", snapshot, token);

                // Also broadcast to "all" subscribers
                await _hubContext.Clients
                    .Group("all")
                    .SendAsync("ReceiveWindowedMetrics", snapshot, token);

                _logger.LogDebug(
                    "Pushed windowed snapshot for {IterationName} (window {WindowSequence})",
                    snapshot.IterationName,
                    snapshot.WindowSequence);

                // Upload to customer's InfluxDB (fire-and-forget, non-blocking)
                // Only master uploads - workers have partial data, master has aggregated metrics
                if (_nodeMetadata.NodeType == NodeType.Master)
                {
                   await _influxDBWriter.UploadWindowedMetricsAsync(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to push windowed snapshot for {IterationId}",
                    snapshot.IterationId);
            }
        }
    }
}
