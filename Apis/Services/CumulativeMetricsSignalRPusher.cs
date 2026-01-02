#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Cumulative;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apis.Services
{
    /// <summary>
    /// Background service that reads cumulative metric snapshots from the queue
    /// and pushes them to connected SignalR clients.
    /// Cumulative data is pushed at its own interval (RefreshRate), separate from windowed data.
    /// </summary>
    public sealed class CumulativeMetricsSignalRPusher : BackgroundService
    {
        private readonly ICumulativeMetricsQueue _queue;
        private readonly IHubContext<WindowedMetricsHub> _hubContext;
        private readonly ILogger<CumulativeMetricsSignalRPusher> _logger;

        public CumulativeMetricsSignalRPusher(
            ICumulativeMetricsQueue queue,
            IHubContext<WindowedMetricsHub> hubContext,
            ILogger<CumulativeMetricsSignalRPusher> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
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
                    await PushSnapshotAsync(snapshot, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("CumulativeMetricsSignalRPusher stopping due to cancellation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CumulativeMetricsSignalRPusher encountered an error.");
                throw;
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to push cumulative snapshot for {IterationId}",
                    snapshot.IterationId);
            }
        }
    }
}
