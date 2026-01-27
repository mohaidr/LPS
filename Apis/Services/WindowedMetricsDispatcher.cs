#nullable enable
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.Infrastructure.Nodes;
using LPS.UI.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Apis.Services
{
    /// <summary>
    /// Background service that reads windowed metric snapshots from the queue
    /// and pushes them to connected SignalR clients and customer's InfluxDB.
    /// Clean separation: just reads and forwards, no knowledge of window timing.
    /// Only master node uploads to InfluxDB (workers have partial data).
    /// </summary>
    public sealed class WindowedMetricsDispatcher : BackgroundService
    {
        private readonly IWindowedMetricsQueue _queue;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly IInfluxDBWriter _influxDBWriter;
        private readonly INodeMetadata _nodeMetadata;
        private readonly IWindowedMetricsCoordinator _coordinator;
        private readonly int _refreshRateMs;
        private readonly ILogger<WindowedMetricsDispatcher> _logger;

        public WindowedMetricsDispatcher(
            IWindowedMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            IInfluxDBWriter influxDBWriter,
            INodeMetadata nodeMetadata,
            IWindowedMetricsCoordinator coordinator,
            IConfiguration configuration,
            ILogger<WindowedMetricsDispatcher> logger)
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
            _logger.LogInformation("WindowedMetricsDispatcher started, listening for snapshots.");

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
                _logger.LogError(ex, "WindowedMetricsDispatcher encountered an error.");
                throw;
            }
            finally
            {
                await DrainQueueAsync();
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


        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            await DrainQueueAsync();
            _logger.LogInformation("WindowedMetricsDispatcher stopped.");
            await base.StopAsync(stoppingToken);
        }

        async ValueTask DrainQueueAsync()
        {
            // Skip drain logic for config commands - no metrics to drain
            if (!CommandContext.IsTestExecutionCommand)
            {
                _logger.LogDebug("WindowedMetricsDispatcher: Skipping drain for config command.");
                return;
            }

            // Wait to ensure final data is in the queue - upon cancellation there is a very high probability of race condition so we have to wait
            await Task.Delay(_refreshRateMs);
            while (_queue.Reader.TryRead(out var snapshot))
            {
                _logger.LogDebug(
                    "WindowedMetricsDispatcher: Draining snapshot for IterationId={IterationId}, IsFinal={IsFinal}",
                    snapshot.IterationId, snapshot.IsFinal);
                await PushSnapshotAsync(snapshot, CancellationToken.None);
            }
        }
    }
}
