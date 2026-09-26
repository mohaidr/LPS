#nullable enable
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Monitoring.Windowed;
using LPS.Infrastructure.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
    public sealed class WindowedMetricsDispatcher : MetricsDispatcher<WindowedIterationSnapshot>
    {
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly IInfluxDBWriter _influxDBWriter;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ILogger<WindowedMetricsDispatcher> _logger;

        public WindowedMetricsDispatcher(
            IWindowedMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            IInfluxDBWriter influxDBWriter,
            INodeMetadata nodeMetadata,
            ILogger<WindowedMetricsDispatcher> logger)
            : base(queue.Reader, queue.Complete)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _influxDBWriter = influxDBWriter ?? throw new ArgumentNullException(nameof(influxDBWriter));
            _nodeMetadata = nodeMetadata ?? throw new ArgumentNullException(nameof(nodeMetadata));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task PushSnapshotAsync(WindowedIterationSnapshot snapshot, CancellationToken token)
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

                // Upload to customer's InfluxDB
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
