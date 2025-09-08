using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;

namespace LPS.Infrastructure.Monitoring.MetricsServices
{
    /// <summary>
    /// Thread-safe factory+cache for metric aggregators per iteration.
    /// Uses iteration.Id (Guid) as stable key.
    /// </summary>
    public sealed class MetricAggregatorFactory : IMetricAggregatorFactory, IDisposable
    {
        private readonly Lazy<IMetricsQueryService> _metricsQueryService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _op;
        private readonly IMetricsVariableService _metricsVarSvc;

        private sealed record Entry(HttpIteration Iteration, IReadOnlyList<IMetricAggregator> Aggregators);

        private readonly ConcurrentDictionary<Guid, Lazy<Entry>> _cache =
            new(Environment.ProcessorCount, 31);

        public MetricAggregatorFactory(
           Lazy<IMetricsQueryService> metricsQueryService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricsVariableService metricsVariableService)
        {
            _metricsQueryService = metricsQueryService ?? throw new ArgumentNullException(nameof(metricsQueryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _op = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _metricsVarSvc = metricsVariableService ?? throw new ArgumentNullException(nameof(metricsVariableService));
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
            return new List<IMetricAggregator>
            {
                new ResponseCodeMetricAggregator(httpIteration, roundName, _logger, _op, _metricsVarSvc),
                new DurationMetricAggregator(httpIteration, roundName, _logger, _op, _metricsVarSvc),
                new ThroughputMetricAggregator(httpIteration, roundName, _metricsQueryService.Value, _logger, _op, _metricsVarSvc),
                new DataTransmissionMetricAggregator(httpIteration, roundName, _metricsQueryService.Value, _logger, _op, _metricsVarSvc)
            };
        }

        private static void DisposeAggregators(IReadOnlyList<IMetricAggregator> aggregators)
        {
            foreach (var a in aggregators)
                if (a is IDisposable d) d.Dispose();
        }

        public void Dispose() => Clear(dispose: true);
    }
}
