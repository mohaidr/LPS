// MetricsDataMonitor.cs
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class MetricsDataMonitor : IMetricsDataMonitor, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IMonitoredRunRepository _monitoredRunRepository;

        public MetricsDataMonitor(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMonitoredRunRepository monitoredRunRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
            _monitoredRunRepository = monitoredRunRepository ?? throw new ArgumentNullException(nameof(monitoredRunRepository));
        }

        public bool TryRegister(HttpRun httpRun)
        {
            if (_monitoredRunRepository.MonitoredRuns.ContainsKey(httpRun))
            {
                return false;
            }

            var metrics = CreateMetricCollectors(httpRun);
            var monitoredRun = new MonitoredHttpRun(httpRun, metrics);

            return _monitoredRunRepository.MonitoredRuns.TryAdd(httpRun, monitoredRun);
        }

        private IReadOnlyDictionary<string, IMetricCollector> CreateMetricCollectors(HttpRun httpRun)
        {
            return new Dictionary<string, IMetricCollector>
            {
                { $"{httpRun.Id}-BreakDown", new ResponseCodeMetricCollector(httpRun, _logger, _runtimeOperationIdProvider) },
                { $"{httpRun.Id}-Duration", new DurationMetricCollector(httpRun, _logger, _runtimeOperationIdProvider) },
                { $"{httpRun.Id}-Throughput", new ThroughputMetricCollector(httpRun, _logger, _runtimeOperationIdProvider) },
                { $"{httpRun.Id}-DataTransmission", new DataTransmissionMetricCollector(httpRun, _logger, _runtimeOperationIdProvider) }
            };
        }

        public void Monitor(HttpRun httpRun, string executionId)
        {
            var monitoredRun = _monitoredRunRepository.MonitoredRuns.GetOrAdd(httpRun, run =>
            {
                var metrics = CreateMetricCollectors(run);
                return new MonitoredHttpRun(run, metrics);
            });

            lock (monitoredRun.ExecutionIds)
            {
                if (!monitoredRun.ExecutionIds.Contains(executionId))
                {
                    if (!monitoredRun.ExecutionIds.Any())
                    {
                        foreach (var metric in monitoredRun.Metrics.Values)
                        {
                            metric.Start();
                        }
                    }
                    monitoredRun.ExecutionIds.Add(executionId);
                }
            }
        }

        public void Stop(HttpRun httpRun, string executionId)
        {
            if (_monitoredRunRepository.MonitoredRuns.TryGetValue(httpRun, out var monitoredRun))
            {
                lock (monitoredRun.ExecutionIds)
                {
                    var executionIds = monitoredRun.ExecutionIds.ToList();
                    executionIds.Remove(executionId);
                    monitoredRun.ExecutionIds.Clear();
                    foreach (var id in executionIds)
                    {
                        monitoredRun.ExecutionIds.Add(id);
                    }

                    if (monitoredRun.ExecutionIds.IsEmpty)
                    {
                        foreach (var metric in monitoredRun.Metrics.Values)
                        {
                            metric.Stop();
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (var monitoredRun in _monitoredRunRepository.MonitoredRuns.Values)
            {
                foreach (var metricCollector in monitoredRun.Metrics.Values)
                {
                    if (metricCollector is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }
    }
}
