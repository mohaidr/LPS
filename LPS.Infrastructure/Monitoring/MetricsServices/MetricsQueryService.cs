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
        IMetricAggregatorFactory aggregatorFactory) : IMetricsQueryService
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
        private readonly IMetricAggregatorFactory _factory = aggregatorFactory ?? throw new ArgumentNullException(nameof(aggregatorFactory));

        public async ValueTask<List<IMetricAggregator>> GetAsync(Func<IMetricAggregator, bool> predicate, CancellationToken token)
        {
            try
            {
                return EnumerateAllAggregators()
                    .Where(predicate)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics.\n{ex}", LPSLoggingLevel.Error, token);
                return null;
            }
        }

        public async ValueTask<List<T>> GetAsync<T>(Func<T, bool> predicate, CancellationToken token) where T : IMetricAggregator
        {
            try
            {
                return EnumerateAllAggregators()
                    .OfType<T>()
                    .Where(predicate)
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics.\n{ex}", LPSLoggingLevel.Error, token);
                return null;
            }
        }

        public async ValueTask<List<T>> GetAsync<T>(Func<T, Task<bool>> predicate, CancellationToken token) where T : IMetricAggregator
        {
            try
            {
                var result = new List<T>();
                var all = EnumerateAllAggregators().OfType<T>();

                foreach (var item in all)
                {
                    if (await predicate(item))
                        result.Add(item);
                }

                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Failed to get metrics with async predicate.\n{ex}", LPSLoggingLevel.Error, token);
                return null;
            }
        }

        // Snapshot enumerator over all aggregators across registered iterations
        private IEnumerable<IMetricAggregator> EnumerateAllAggregators()
        {
            // _factory.Iterations is a safe snapshot of registered iterations
            foreach (var iteration in _factory.Iterations.ToList())
            {
                if (_factory.TryGet(iteration.Id, out var aggregators) && aggregators is not null)
                {
                    foreach (var a in aggregators)
                        yield return a;
                }
            }
        }
    }
}
