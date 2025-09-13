using LPS.Common.Interfaces;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.UI.Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Core.Services
{

    public sealed class MetricsUiService : IMetricsUiService
    {
        private readonly IMetricDataStore _store;
        private readonly IIterationStatusMonitor _status;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly ILogger _logger;

        public MetricsUiService(
            IMetricDataStore metricDataStore,
            IIterationStatusMonitor iterationStatusMonitor,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            ILogger logger)
        {
            _store = metricDataStore ?? throw new ArgumentNullException(nameof(metricDataStore));
            _status = iterationStatusMonitor ?? throw new ArgumentNullException(nameof(iterationStatusMonitor));
            _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<IReadOnlyList<MetricDataDto>> LatestForIterationAsync(Guid iterationId, CancellationToken token = default) =>
            QueryAsync(new MetricsQuery(IterationId: iterationId), token);

        public Task<IReadOnlyList<MetricDataDto>> LatestForRoundAsync(string roundName, CancellationToken token = default) =>
            QueryAsync(new MetricsQuery(RoundName: roundName), token);

        public Task<IReadOnlyList<MetricDataDto>> LatestByMetricTypeAsync(LPSMetricType metricType, CancellationToken token = default) =>
            QueryAsync(new MetricsQuery(MetricType: metricType), token);

        public async Task<IReadOnlyList<MetricDataDto>> QueryAsync(MetricsQuery q, CancellationToken token = default)
        {
            var results = new List<MetricDataDto>();

            // Iterate only the requested iteration if provided; otherwise, go over all
            IEnumerable<HttpIteration> iterations = _store.Iterations;
            if (q.IterationId is Guid idFilter)
                iterations = iterations.Where(i => i.Id == idFilter);

            foreach (var iteration in iterations)
            {
                // Grab the latest snapshot(s) we need
                _store.TryGetLatest<ResponseCodeMetricSnapshot>(iteration.Id, LPSMetricType.ResponseCode, out var respCode);
                _store.TryGetLatest<DurationMetricSnapshot>(iteration.Id, LPSMetricType.ResponseTime, out var duration);
                _store.TryGetLatest<ThroughputMetricSnapshot>(iteration.Id, LPSMetricType.Throughput, out var throughput);
                _store.TryGetLatest<DataTransmissionMetricSnapshot>(iteration.Id, LPSMetricType.DataTransmission, out var dataTx);

                // If the caller asked to filter by RoundName, do it using the snapshot metadata.
                if (!string.IsNullOrWhiteSpace(q.RoundName))
                {
                    var anyForRound = GetAnySnapshot(respCode, duration, throughput, dataTx);
                    // If we still don't have a snapshot, this iteration has no data yet → skip
                    if (anyForRound is null) continue;

                    if (!string.Equals(anyForRound.RoundName, q.RoundName, StringComparison.OrdinalIgnoreCase))
                        continue; // round doesn't match → skip
                }

                // If caller asked for a specific metric type, null-out others (no tuple switch)
                if (q.MetricType is LPSMetricType only)
                {
                    if (only != LPSMetricType.ResponseCode) respCode = null;
                    if (only != LPSMetricType.ResponseTime) duration = null;
                    if (only != LPSMetricType.Throughput) throughput = null;
                    if (only != LPSMetricType.DataTransmission) dataTx = null;
                }

                // If we have no snapshots left after filtering, skip this iteration
                if (respCode is null && duration is null && throughput is null && dataTx is null)
                    continue;

                // Use any available snapshot to fill common fields; otherwise fall back to iteration metadata
                HttpMetricSnapshot? any =
                    respCode as HttpMetricSnapshot
                    ?? duration as HttpMetricSnapshot
                    ?? throughput as HttpMetricSnapshot
                    ?? dataTx as HttpMetricSnapshot;

                var status = (await _status.GetTerminalStatusAsync(iteration)).ToString();

                var dto = new MetricDataDto
                {
                    ExecutionStatus = status,
                    TimeStamp = any?.TimeStamp ?? DateTime.UtcNow,
                    RoundName = any?.RoundName ?? string.Empty,
                    IterationId = any?.IterationId ?? iteration.Id,
                    IterationName = any?.IterationName ?? iteration.Name, // if IterationName is not set on snapshot, we fall back
                    URL = any?.URL ?? iteration.HttpRequest?.Url?.Url ?? string.Empty,
                    HttpMethod = any?.HttpMethod ?? iteration.HttpRequest?.HttpMethod ?? string.Empty,
                    HttpVersion = any?.HttpVersion ?? iteration.HttpRequest?.HttpVersion ?? string.Empty,
                    Endpoint = $"{any?.IterationName ?? iteration.Name} {any?.URL ?? iteration.HttpRequest?.Url?.Url} HTTP/{any?.HttpVersion ?? iteration.HttpRequest?.HttpVersion}",

                    ResponseBreakDownMetrics = respCode,
                    ResponseTimeMetrics = duration,
                    ConnectionMetrics = throughput,
                    DataTransmissionMetrics = dataTx
                };

                results.Add(dto);
            }

            return results;
        }

        private static HttpMetricSnapshot? GetAnySnapshot(
            ResponseCodeMetricSnapshot? respCode,
            DurationMetricSnapshot? duration,
            ThroughputMetricSnapshot? throughput,
            DataTransmissionMetricSnapshot? dataTx)
        {
            return respCode as HttpMetricSnapshot
                ?? duration as HttpMetricSnapshot
                ?? throughput as HttpMetricSnapshot
                ?? dataTx as HttpMetricSnapshot;
        }
    }

}
