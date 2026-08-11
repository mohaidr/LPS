#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Hosts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apis.Services
{
    public sealed class HostCumulativeMetricsDispatcher : BackgroundService
    {
        private readonly IHostCumulativeMetricsQueue _queue;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly ILogger<HostCumulativeMetricsDispatcher> _logger;

        public HostCumulativeMetricsDispatcher(
            IHostCumulativeMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            ILogger<HostCumulativeMetricsDispatcher> logger)
        {
            _queue = queue;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var snapshot in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    await _hubContext.Clients
                        .Group("all")
                        .SendAsync("ReceiveCumulativeHostMetrics", snapshot, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Host cumulative metrics dispatcher encountered an error.");
                throw;
            }
        }
    }
}