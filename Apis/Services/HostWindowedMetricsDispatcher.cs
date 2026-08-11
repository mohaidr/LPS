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
    public sealed class HostWindowedMetricsDispatcher : BackgroundService
    {
        private readonly IHostWindowedMetricsQueue _queue;
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly ILogger<HostWindowedMetricsDispatcher> _logger;

        public HostWindowedMetricsDispatcher(
            IHostWindowedMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            ILogger<HostWindowedMetricsDispatcher> logger)
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
                        .SendAsync("ReceiveWindowedHostMetrics", snapshot, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Host windowed metrics dispatcher encountered an error.");
                throw;
            }
        }
    }
}