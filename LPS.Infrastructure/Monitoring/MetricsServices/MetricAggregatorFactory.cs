using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Monitoring.Windowed;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Thread-safe factory+cache for metric aggregators per iteration.
    /// Uses iteration.Id (Guid) as stable key.
    /// Creates both cumulative and windowed aggregators.
    /// Collectors are created separately by MetricsDataMonitor.
    /// </summary>
    public sealed class MetricAggregatorFactory : IMetricAggregatorFactory, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly IMetricsVariableService _metricsVarSvc;
        private readonly ILiveMetricDataStore _metricDataStore;
        private readonly IRuleService _rulesService;

        private sealed record Entry(HttpIteration Iteration, IReadOnlyList<IMetricAggregator> Aggregators);

        private readonly ConcurrentDictionary<Guid, Lazy<Entry>> _cache =
            new(Environment.ProcessorCount, 31);

        public MetricAggregatorFactory(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService,
            ILiveMetricDataStore metricDataStore,
            IRuleService rulesService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVarSvc = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
            _metricDataStore = metricDataStore ?? throw new ArgumentNullException(nameof(metricDataStore));
            _rulesService = rulesService ?? throw new ArgumentNullException(nameof(rulesService));
        }

        public IReadOnlyList<IMetricAggregator> GetOrCreate(HttpIteration iteration, string roundName)
        {
            if (iteration is null) throw new ArgumentNullException(nameof(iteration));
            if (string.IsNullOrWhiteSpace(roundName)) throw new ArgumentException("Required", nameof(roundName));

            var lazy = _cache.GetOrAdd(iteration.Id, _ =>
                new Lazy<Entry>(() => new Entry(
                    iteration,
                    CreateAggregators(roundName, iteration)),
                    isThreadSafe: true));

            return lazy.Value.Aggregators;
        }

        public bool TryGet(Guid iterationId, out IReadOnlyList<IMetricAggregator> aggregators)
        {
            aggregators = null!;
            if (_cache.TryGetValue(iterationId, out var lazy))
            {
                var v = lazy.Value;
                aggregators = v.Aggregators;
                return true;
            }
            return false;
        }

        public IEnumerable<HttpIteration> Iterations =>
            _cache.Values.Where(l => l.IsValueCreated).Select(l => l.Value.Iteration);

        // Do not use the remove before we separate the metrics data from the aggregator
        // Because of a bad design, currently removing the aggregators means we have no reach to the metrics data snapshots
        public bool Remove(Guid iterationId, bool dispose = true)
        {
            if (_cache.TryRemove(iterationId, out var lazy))
            {
                if (dispose && lazy.IsValueCreated)
                    DisposeAggregators(lazy.Value.Aggregators);
                return true;
            }
            return false;
        }

        public void Clear(bool dispose = true)
        {
            var entries = _cache.Values.Where(v => v.IsValueCreated).Select(v => v.Value).ToList();
            _cache.Clear();

            if (dispose)
            {
                foreach (var e in entries) DisposeAggregators(e.Aggregators);
            }
        }

        private IReadOnlyList<IMetricAggregator> CreateAggregators(string roundName, HttpIteration httpIteration)
        {
            // Cumulative aggregators (these write to the metric data store)
            // Note: Stopwatches in ThroughputMetricAggregator and DataTransmissionMetricAggregator
            // are started lazily on first data record, not at creation time.
            var cumulative = new List<IMetricAggregator>
            {
                new ResponseCodeMetricAggregator(httpIteration, roundName, _logger, _op, _metricsVarSvc, _metricDataStore),
                new DurationMetricAggregator(httpIteration, roundName, _logger, _op, _metricsVarSvc, _metricDataStore),
                new ThroughputMetricAggregator(httpIteration, roundName , _logger, _op, _metricsVarSvc, _rulesService, _metricDataStore),
                new DataTransmissionMetricAggregator(httpIteration, roundName, _logger, _op, _metricsVarSvc, _metricDataStore)
            };

            // Windowed aggregators (lightweight - no queue/coordinator dependency)
            var windowedThroughput = new WindowedThroughputAggregator(httpIteration, roundName);
            var windowedResponseCode = new WindowedResponseCodeAggregator(httpIteration, roundName, _rulesService);
            
            // Wire up response code -> throughput for success/failure tracking based on failure rules
            windowedResponseCode.SetThroughputAggregator(windowedThroughput);
            
            var windowed = new List<IMetricAggregator>
            {
                new WindowedDurationAggregator(httpIteration, roundName),
                windowedThroughput,
                windowedResponseCode,
                new WindowedDataTransmissionAggregator(httpIteration, roundName)
            };

            // Return all aggregators
            var all = new List<IMetricAggregator>(cumulative.Count + windowed.Count);
            all.AddRange(cumulative);
            all.AddRange(windowed);
            return all;
        }

        private static void DisposeAggregators(IReadOnlyList<IMetricAggregator> aggregators)
        {
            foreach (var a in aggregators)
                if (a is IDisposable d) d.Dispose();
        }

        public void Dispose() => Clear(dispose: true);
    }
}
