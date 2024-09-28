using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Logger;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.LPSClients.MetricsServices;

namespace LPS.Infrastructure.LPSClients.Metrics
{
    public class MetricsService : IMetricsService
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private static readonly ConcurrentDictionary<string, IList<IMetricMonitor>> _metrics = new ConcurrentDictionary<string, IList<IMetricMonitor>>();

        public MetricsService(ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public async Task AddMetricsAsync(Guid requestId)
        {
            _metrics.TryAdd(requestId.ToString(), await MetricsDataMonitor.GetAsync(metric => metric.LPSHttpRun.LPSHttpRequestProfile.Id == requestId));
        }
        private IEnumerable<IMetricMonitor> GetConnectionsMetrics(Guid requestId)
        {
            return _metrics[requestId.ToString()]
                    .Where(metric => metric.MetricType == LPSMetricType.ConnectionsCount);
        }
        public async Task<bool> TryIncreaseConnectionsCountAsync(HttpRequestProfile lpsHttpRequestProfile, CancellationToken token)
        {
            try
            {
                var connectionsMetrics = GetConnectionsMetrics(lpsHttpRequestProfile.Id);

                foreach (var metric in connectionsMetrics)
                {
                    ((IThroughputMetricMonitor)metric).IncreaseConnectionsCount();
                }
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                                       $"Failed to increase connections metrics\n{(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}",
                                       LPSLoggingLevel.Error, token);
                return false;
            }
        }

        public async Task<bool> TryDecreaseConnectionsCountAsync(HttpRequestProfile lpsHttpRequestProfile, bool isSuccessful, CancellationToken token)
        {
            try
            {
                var connectionsMetrics = GetConnectionsMetrics(lpsHttpRequestProfile.Id);
                foreach (var metric in connectionsMetrics)
                {
                    ((IThroughputMetricMonitor)metric).DecreseConnectionsCount(isSuccessful);
                }
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                                       $"Failed to decrease connections metrics\n{(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}",
                                       LPSLoggingLevel.Error, token);
                return false;
            }
        }

        public async Task<bool> TryUpdateResponseMetricsAsync(HttpRequestProfile lpsHttpRequestProfile, HttpResponse lpsResponse, CancellationToken token)
        {
            try
            {
                var responsMetrics = _metrics[lpsHttpRequestProfile.Id.ToString()].Where(metric => metric.MetricType == LPSMetricType.ResponseTime || metric.MetricType == LPSMetricType.ResponseCode);
                await Task.WhenAll(responsMetrics.Select(metric => ((IResponseMetric)metric).UpdateAsync(lpsResponse)));
                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to update connections metrics\n{(ex.InnerException != null ? ex.InnerException.Message : string.Empty)} \n\t  {ex.Message} \n  {ex.StackTrace}", LPSLoggingLevel.Error, token);
                return false;
            }

        }
    }
}
