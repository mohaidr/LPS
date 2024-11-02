// MetricsDataMonitor.cs
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class MetricsDataMonitor(
        ILogger logger,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMonitoredIterationRepository monitoredRunRepository) : IMetricsDataMonitor, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
        private readonly IMonitoredIterationRepository _monitoredIterationsRepository = monitoredRunRepository ?? throw new ArgumentNullException(nameof(monitoredRunRepository));

        public bool TryRegister(string roundName, HttpIteration httpIteration)
        {
            try
            {
                if (_monitoredIterationsRepository.MonitoredIterations.ContainsKey(httpIteration))
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, $"Iteration has already been registered. Below are the iteration details: \r\nRound:{roundName} \r\nIteration: {httpIteration.Name}", LPSLoggingLevel.Verbose);
                    return false;
                }

                var metrics = CreateMetricCollectors(roundName, httpIteration);
                var monitoredIteration = new MonitoredHttpIteration(httpIteration, metrics);

                return _monitoredIterationsRepository.MonitoredIterations.TryAdd(httpIteration, monitoredIteration);
            }
            catch(Exception ex)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Failed to register http iteration. Below are the exception details: \r\nRound:{roundName} \r\nIteration: {httpIteration.Name} \r\nException:{ex.Message}", LPSLoggingLevel.Error);
                return false;
            }
        }

        private IReadOnlyDictionary<string, IMetricCollector> CreateMetricCollectors(string roundName, HttpIteration httpIteration)
        {
            return new Dictionary<string, IMetricCollector>
            {
                { $"{httpIteration.Id}-BreakDown", new ResponseCodeMetricCollector(httpIteration,roundName, _logger, _runtimeOperationIdProvider) },
                { $"{httpIteration.Id}-Duration", new DurationMetricCollector(httpIteration,roundName, _logger, _runtimeOperationIdProvider) },
                { $"{httpIteration.Id}-Throughput", new ThroughputMetricCollector(httpIteration,roundName, _logger, _runtimeOperationIdProvider) },
                { $"{httpIteration.Id}-DataTransmission", new DataTransmissionMetricCollector(httpIteration,roundName, _logger, _runtimeOperationIdProvider) }
            };
        }

        public void Monitor(HttpIteration httpIteration, string executionId)
        {
           bool iterationRegistered = _monitoredIterationsRepository.MonitoredIterations.TryGetValue(httpIteration, out MonitoredHttpIteration monitoredIteration);
            if (!iterationRegistered)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Monitoring can't start. iteration {httpIteration.Name} has not been registered yet.", LPSLoggingLevel.Error);
                return;
            }

            lock (monitoredIteration.ExecutionIds)
            {
                if (!monitoredIteration.ExecutionIds.Contains(executionId))
                {
                    if (!monitoredIteration.ExecutionIds.Any())
                    {
                        foreach (var metric in monitoredIteration.Metrics.Values)
                        {
                            metric.Start();
                        }
                    }
                    monitoredIteration.ExecutionIds.Add(executionId);
                }
            }
        }

        public void Stop(HttpIteration httpIteration, string executionId)
        {
            if (_monitoredIterationsRepository.MonitoredIterations.TryGetValue(httpIteration, out var monitoredIteration))
            {
                lock (monitoredIteration.ExecutionIds)
                {
                    var executionIds = monitoredIteration.ExecutionIds.ToList();
                    executionIds.Remove(executionId);
                    monitoredIteration.ExecutionIds.Clear();
                    foreach (var id in executionIds)
                    {
                        monitoredIteration.ExecutionIds.Add(id);
                    }

                    if (monitoredIteration.ExecutionIds.IsEmpty)
                    {
                        foreach (var metric in monitoredIteration.Metrics.Values)
                        {
                            metric.Stop();
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (var monitoredIteration in _monitoredIterationsRepository.MonitoredIterations.Values)
            {
                foreach (var metricCollector in monitoredIteration.Metrics.Values)
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
