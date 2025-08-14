// MetricsDataService.cs
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    public class MetricsQueryService(
        ILogger logger,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMetricsRepository metricsRepository) : IMetricsQueryService
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
        private readonly IMetricsRepository _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));

        public async ValueTask<List<IMetricCollector>> GetAsync(Func<IMetricCollector, bool> predicate, CancellationToken token)
        {
            try
            {
                return _metricsRepository.Data.Values
                    .SelectMany(metricsContainer => metricsContainer.Metrics)
                    .Where(predicate)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics.\n{ex}", LPSLoggingLevel.Error, token);
                return null;
            }
        }

        public async ValueTask<List<T>> GetAsync<T>(Func<T, bool> predicate, CancellationToken token) where T : IMetricCollector
        {
            try
            {
                return _metricsRepository.Data.Values
                    .SelectMany(metricsContainer => metricsContainer.Metrics.OfType<T>())
                    .Where(predicate)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics.\n{ex}", LPSLoggingLevel.Error, token);
                return null;
            }
        }

        public async Task<List<T>> GetAsync<T>(Func<T, Task<bool>> predicate, CancellationToken token) where T : IMetricCollector
        {
            try
            {
                var result = new List<T>();
                var all = _metricsRepository.Data.Values
                    .SelectMany(metricsContainer => metricsContainer.Metrics.OfType<T>());

                foreach (var item in all)
                {
                    if (await predicate(item))
                    {
                        result.Add(item);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics with async predicate.\n{ex}", LPSLoggingLevel.Error, token);
                return null;
            }
        }
    }
}
