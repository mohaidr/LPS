#nullable enable
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Cumulative;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Monitoring.Windowed;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Monitor that registers iterations for metrics collection.
    /// Creates both aggregators (via factory) and collectors (windowed + cumulative).
    /// Collectors self-manage their lifecycle via IIterationStatusMonitor:
    /// - They start collecting immediately on registration
    /// - They check iteration status on each coordinator tick
    /// - They auto-stop and flush when iteration reaches a terminal status
    /// </summary>
    public class MetricsDataMonitor : IMetricsDataMonitor, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly IMetricAggregatorFactory _factory;
        private readonly IWindowedMetricsQueue _windowedQueue;
        private readonly IWindowedMetricDataStore _windowedDataStore;
        private readonly IWindowedMetricsCoordinator _windowedCoordinator;
        private readonly ICumulativeMetricsQueue _cumulativeQueue;
        private readonly ICumulativeMetricDataStore _cumulativeDataStore;
        private readonly ICumulativeMetricsCoordinator _cumulativeCoordinator;
        private readonly IIterationStatusMonitor _iterationStatusMonitor;
        private readonly IPlanExecutionContext _planContext;

        // Track collectors for lifecycle management
        private readonly ConcurrentDictionary<Guid, WindowedIterationMetricsCollector> _windowedCollectors = new();
        private readonly ConcurrentDictionary<Guid, CumulativeIterationMetricsCollector> _cumulativeCollectors = new();

        public MetricsDataMonitor(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricAggregatorFactory aggregatorFactory,
            IWindowedMetricsQueue windowedQueue,
            IWindowedMetricDataStore windowedDataStore,
            IWindowedMetricsCoordinator windowedCoordinator,
            ICumulativeMetricsQueue cumulativeQueue,
            ICumulativeMetricDataStore cumulativeDataStore,
            ICumulativeMetricsCoordinator cumulativeCoordinator,
            IIterationStatusMonitor iterationStatusMonitor,
            IPlanExecutionContext planContext)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _factory = aggregatorFactory ?? throw new ArgumentNullException(nameof(aggregatorFactory));
            _windowedQueue = windowedQueue ?? throw new ArgumentNullException(nameof(windowedQueue));
            _windowedDataStore = windowedDataStore ?? throw new ArgumentNullException(nameof(windowedDataStore));
            _windowedCoordinator = windowedCoordinator ?? throw new ArgumentNullException(nameof(windowedCoordinator));
            _cumulativeQueue = cumulativeQueue ?? throw new ArgumentNullException(nameof(cumulativeQueue));
            _cumulativeDataStore = cumulativeDataStore ?? throw new ArgumentNullException(nameof(cumulativeDataStore));
            _cumulativeCoordinator = cumulativeCoordinator ?? throw new ArgumentNullException(nameof(cumulativeCoordinator));
            _iterationStatusMonitor = iterationStatusMonitor ?? throw new ArgumentNullException(nameof(iterationStatusMonitor));
            _planContext = planContext ?? throw new ArgumentNullException(nameof(planContext));
        }

        /// <summary>
        /// Registers an iteration for metrics collection. 
        /// Creates aggregators via factory and collectors for windowed + cumulative metrics.
        /// Collectors automatically start listening to coordinator events.
        /// They will self-manage their lifecycle based on IIterationStatusMonitor.
        /// </summary>
        public async ValueTask<bool> TryRegisterAsync(string roundName, HttpIteration httpIteration)
        {
            try
            {
                // Check if already registered
                if (_factory.TryGet(httpIteration.Id, out _))
                {
                    await _logger.LogAsync(_op.OperationId, $"Iteration already registered.\nRound: {roundName}\nIteration: {httpIteration.Name}", LPSLoggingLevel.Verbose);
                    return false;
                }

                // Get or create all aggregators from factory
                var aggregators = _factory.GetOrCreate(httpIteration, roundName);

                // Extract windowed aggregators by type
                var windowedDuration = aggregators.OfType<WindowedDurationAggregator>().FirstOrDefault();
                var windowedThroughput = aggregators.OfType<WindowedThroughputAggregator>().FirstOrDefault();
                var windowedResponseCode = aggregators.OfType<WindowedResponseCodeAggregator>().FirstOrDefault();
                var windowedDataTransmission = aggregators.OfType<WindowedDataTransmissionAggregator>().FirstOrDefault();

                // Extract cumulative aggregators by type
                var cumulativeThroughput = aggregators.OfType<ThroughputMetricAggregator>().FirstOrDefault();
                var cumulativeDuration = aggregators.OfType<DurationMetricAggregator>().FirstOrDefault();
                var cumulativeResponseCode = aggregators.OfType<ResponseCodeMetricAggregator>().FirstOrDefault();
                var cumulativeDataTransmission = aggregators.OfType<DataTransmissionMetricAggregator>().FirstOrDefault();

                // Create windowed collector and wire up aggregators
                var windowedCollector = new WindowedIterationMetricsCollector(
                    httpIteration, roundName, _windowedQueue, _windowedDataStore, _windowedCoordinator, _iterationStatusMonitor, _planContext)
                {
                    DurationAggregator = windowedDuration,
                    ThroughputAggregator = windowedThroughput,
                    ResponseCodeAggregator = windowedResponseCode,
                    DataTransmissionAggregator = windowedDataTransmission
                };

                // Create cumulative collector and wire up aggregators (similar to windowed pattern)
                var cumulativeCollector = new CumulativeIterationMetricsCollector(
                    httpIteration, roundName, _cumulativeQueue, _cumulativeDataStore, _cumulativeCoordinator, _iterationStatusMonitor, _planContext)
                {
                    ThroughputAggregator = cumulativeThroughput,
                    DurationAggregator = cumulativeDuration,
                    ResponseCodeAggregator = cumulativeResponseCode,
                    DataTransmissionAggregator = cumulativeDataTransmission
                };

                // Store collectors for lifecycle management
                _windowedCollectors[httpIteration.Id] = windowedCollector;
                _cumulativeCollectors[httpIteration.Id] = cumulativeCollector;

                return true;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"Failed to register iteration.\nRound: {roundName}\nIteration: {httpIteration.Name}\nException: {ex.Message} {ex.InnerException?.Message}", LPSLoggingLevel.Error);
                throw;
            }
        }

        public void Dispose()
        {
            // Dispose collectors
            foreach (var collector in _windowedCollectors.Values)
                collector.Dispose();
            _windowedCollectors.Clear();

            foreach (var collector in _cumulativeCollectors.Values)
                collector.Dispose();
            _cumulativeCollectors.Clear();

            // Dispose aggregators via factory
            _factory.Clear(dispose: true);
        }
    }
}
