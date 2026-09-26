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
    public sealed class HostWindowedMetricsDispatcher : MetricsDispatcher<HostWindowedMetricsSnapshot>
    {
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly ILogger<HostWindowedMetricsDispatcher> _logger;

        public HostWindowedMetricsDispatcher(
            IHostWindowedMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            ILogger<HostWindowedMetricsDispatcher> logger)
            : base(queue.Reader, queue.Complete)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task PushSnapshotAsync(HostWindowedMetricsSnapshot snapshot, CancellationToken token)
        {
            try
            {
                await _hubContext.Clients
                    .Group("all")
                    .SendAsync("ReceiveWindowedHostMetrics", snapshot, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push host windowed snapshot.");
            }
        }

    }
}