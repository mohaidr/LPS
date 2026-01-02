#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Windowed;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apis.Services
{
    /// <summary>
    /// Background service that reads windowed metric snapshots from the queue
    /// and pushes them to connected SignalR clients.
    /// Clean separation: just reads and forwards, no knowledge of window timing.
    /// </summary>
    public sealed class WindowedMetricsSignalRPusher : BackgroundService
    {
        private readonly IWindowedMetricsQueue _queue;
        private readonly IHubContext<WindowedMetricsHub> _hubContext;
        private readonly ILogger<WindowedMetricsSignalRPusher> _logger;

        public WindowedMetricsSignalRPusher(
            IWindowedMetricsQueue queue,
            IHubContext<WindowedMetricsHub> hubContext,
            ILogger<WindowedMetricsSignalRPusher> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
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
