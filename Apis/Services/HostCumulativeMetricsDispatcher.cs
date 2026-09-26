#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Hosts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Apis.Services
{
    public sealed class HostCumulativeMetricsDispatcher : MetricsDispatcher<HostCumulativeMetricsSnapshot>
    {
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly ILogger<HostCumulativeMetricsDispatcher> _logger;

        public HostCumulativeMetricsDispatcher(
            IHostCumulativeMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            ILogger<HostCumulativeMetricsDispatcher> logger)
            : base(queue.Reader, queue.Complete)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task PushSnapshotAsync(HostCumulativeMetricsSnapshot snapshot, CancellationToken token)
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

    }
}