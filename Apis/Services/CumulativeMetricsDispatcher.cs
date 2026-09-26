#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Apis.Hubs;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Apis.Services
{
    /// <summary>
    /// Background service that reads cumulative metric snapshots from the queue
    /// and pushes them to connected SignalR clients and customer's InfluxDB.
    /// Cumulative data is pushed at its own interval (RefreshRate), separate from windowed data.
    /// Only master node uploads to InfluxDB (workers have partial data).
    /// </summary>
    public sealed class CumulativeMetricsDispatcher : MetricsDispatcher<CumulativeIterationSnapshot>
    {
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly IInfluxDBWriter _influxDBWriter;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ILogger<CumulativeMetricsDispatcher> _logger;

        public CumulativeMetricsDispatcher(
            ICumulativeMetricsQueue queue,
            IHubContext<MetricsHub> hubContext,
            IInfluxDBWriter influxDBWriter,
            INodeMetadata nodeMetadata,
            ILogger<CumulativeMetricsDispatcher> logger)
            : base(queue.Reader, queue.Complete)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _influxDBWriter = influxDBWriter ?? throw new ArgumentNullException(nameof(influxDBWriter));
            _nodeMetadata = nodeMetadata ?? throw new ArgumentNullException(nameof(nodeMetadata));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task PushSnapshotAsync(CumulativeIterationSnapshot snapshot, CancellationToken token)
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

                // Upload to customer's InfluxDB
                // Only master uploads - workers have partial data, master has aggregated metrics
                if (_nodeMetadata.NodeType == NodeType.Master)
                {
                    await _influxDBWriter.UploadCumulativeMetricsAsync(snapshot);
                }
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
