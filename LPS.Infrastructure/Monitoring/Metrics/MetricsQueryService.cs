// MetricsDataService.cs
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class MetricsQueryService(
        ILogger logger,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMonitoredIterationRepository monitoredIterationRepository) : IMetricsQueryService
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
        private readonly IMonitoredIterationRepository _monitoredIterationRepository = monitoredIterationRepository ?? throw new ArgumentNullException(nameof(monitoredIterationRepository));

        public async Task<List<IMetricCollector>> GetAsync(Func<IMetricCollector, bool> predicate)
        {
            try
            {
                return _monitoredIterationRepository.MonitoredIterations.Values
                    .SelectMany(iteration => iteration.Metrics.Values)
                    .Where(predicate)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics.\n{ex}", LPSLoggingLevel.Error);
                return null;
            }
        }

        public async Task<List<T>> GetAsync<T>(Func<T, bool> predicate) where T : IMetricCollector
        {
            try
            {
                return _monitoredIterationRepository.MonitoredIterations.Values
                    .SelectMany(iteration => iteration.Metrics.Values.OfType<T>())
                    .Where(predicate)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics.\n{ex}", LPSLoggingLevel.Error);
                return null;
            }
        }
    }
}
