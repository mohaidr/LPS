#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Hosts;
using LPS.UI.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apis.Services
{
    public sealed class HostCumulativeMetricsDispatcher : BackgroundService
    {
        private readonly IHostCumulativeMetricsQueue _queue;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly int _refreshRateMs;
        private readonly ILogger<HostCumulativeMetricsDispatcher> _logger;

        public HostCumulativeMetricsDispatcher(
            IHostCumulativeMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            IConfiguration configuration,
            ILogger<HostCumulativeMetricsDispatcher> logger)
        {
            _queue = queue;
            _hubContext = hubContext;
            var refreshRate = configuration.GetValue<int?>("LPSAppSettings:Dashboard:RefreshRate") ?? 3;
            _refreshRateMs = refreshRate * 1000;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
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
                _logger.LogError(ex, "Host cumulative metrics dispatcher encountered an error.");
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
            await base.StopAsync(stoppingToken);
        }

        private async Task PushSnapshotAsync(HostCumulativeMetricsSnapshot snapshot, CancellationToken token)
        {
            try
            {
                await _hubContext.Clients
                    .Group("all")
                    .SendAsync("ReceiveCumulativeHostMetrics", snapshot, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push host cumulative snapshot.");
            }
        }

        // On cancellation, wait for the host's final (Completed) snapshot to land, then flush it - mirrors the iteration dispatchers.
        private async ValueTask DrainQueueAsync()
        {
            if (!CommandContext.IsTestExecutionCommand)
                return;

            await Task.Delay(_refreshRateMs);
            while (_queue.Reader.TryRead(out var snapshot))
                await PushSnapshotAsync(snapshot, CancellationToken.None);
        }
    }
}