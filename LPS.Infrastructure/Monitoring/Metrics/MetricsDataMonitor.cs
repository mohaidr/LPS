using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Logger;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class MetricsDataMonitor : IMetricsDataMonitor
    {
        private static Domain.Common.Interfaces.ILogger _logger;
        private static IRuntimeOperationIdProvider _lpsRuntimeOperationIdProvider;
        CancellationTokenSource _cts;

        // ConcurrentDictionary to manage both metrics and execution lists with multiple monitors per LPSHttpRun
        private static ConcurrentDictionary<HttpRun, Tuple<IList<string>, Dictionary<string, IMetricMonitor>>> _metrics = new ConcurrentDictionary<HttpRun, Tuple<IList<string>, Dictionary<string, IMetricMonitor>>>();

        public MetricsDataMonitor(Domain.Common.Interfaces.ILogger logger, IRuntimeOperationIdProvider lpsRuntimeOperationIdProvider, CancellationTokenSource cts)
        {
            _logger = logger;
            _lpsRuntimeOperationIdProvider = lpsRuntimeOperationIdProvider;
            _cts = cts;
        }

        public bool TryRegister(HttpRun lpsHttpRun)
        {
            // Check if the key already exists
            if (_metrics.ContainsKey(lpsHttpRun))
            {
                // If it exists, return false indicating the registration did not proceed
                return false;
            }

            // Prepare the metrics dictionary for the given LPSHttpRun
            var metrics = new Dictionary<string, IMetricMonitor>();
            string breakDownMetricKey = $"{lpsHttpRun.Id}-BreakDown";
            string durationMetricKey = $"{lpsHttpRun.Id}-Duration";
            string connectionsMetricKey = $"{lpsHttpRun.Id}-Connections";

            metrics[breakDownMetricKey] = new ResponseCodeMetricMonitor(lpsHttpRun, _logger, _lpsRuntimeOperationIdProvider);
            metrics[durationMetricKey] = new DurationMetricMonitor(lpsHttpRun, _logger, _lpsRuntimeOperationIdProvider);
            metrics[connectionsMetricKey] = new ThroughputMetricMonitor(lpsHttpRun, _cts, _logger, _lpsRuntimeOperationIdProvider);

            // Prepare the tuple to be added
            var metricsTuple = new Tuple<IList<string>, Dictionary<string, IMetricMonitor>>(new List<string>() { }, metrics);
            return _metrics.TryAdd(lpsHttpRun, metricsTuple);
           // Try to add the new entry to the dictionary
        }

        public void Monitor(HttpRun lpsHttpRun, string executionId)
        {
            try
            {
                var tuple = _metrics.GetOrAdd(lpsHttpRun, run =>
                {
                    var metrics = new Dictionary<string, IMetricMonitor>();
                    // Create and start all necessary monitors with unique IDs
                    string breakDownMetricKey = $"{run.Id}-BreakDown";
                    string durationMetricKey = $"{run.Id}-Duration";
                    string connectionsMetricKey = $"{run.Id}-Connections";

                    metrics[breakDownMetricKey] = new ResponseCodeMetricMonitor(run, _logger, _lpsRuntimeOperationIdProvider);
                    metrics[durationMetricKey] = new DurationMetricMonitor(run, _logger, _lpsRuntimeOperationIdProvider);
                    metrics[connectionsMetricKey] = new ThroughputMetricMonitor(run, _cts, _logger, _lpsRuntimeOperationIdProvider);

                    return new Tuple<IList<string>, Dictionary<string, IMetricMonitor>>(new List<string>(), metrics);
                });

                // Add execution ID to the list if it doesn't already exist
                if (!tuple.Item1.Contains(executionId))
                {
                    if (tuple.Item1.Count == 0)
                    {
                        foreach (var metric in tuple.Item2.Values)
                        {
                            metric.Start();
                        }
                    }

                    tuple.Item1.Add(executionId);
                }
            }
            catch
            {
                throw;
            }
        }

        public void Stop(HttpRun lpsHttpRun, string executionId)
        {
            if (_metrics.TryGetValue(lpsHttpRun, out Tuple<IList<string>, Dictionary<string, IMetricMonitor>> tuple))
            {
                // Remove the execution ID
                tuple.Item1.Remove(executionId);

                // If no more executions are linked, dispose of all metric monitors and remove from the dictionary
                if (tuple.Item1.Count == 0)
                {
                    foreach (var metric in tuple.Item2.Values)
                    {
                        metric.Stop(); // Stop monitoring
                    }
                }
            }
        }

        public static List<IMetricMonitor> Get(Func<IMetricMonitor, bool> predicate)
        {
            try
            {

                // Return all metric monitors matching the predicate
                return _metrics.Values
                                  .SelectMany(x => x.Item2.Values)
                                  .Where(predicate)
                                  .ToList();
            }
            catch (Exception ex)
            {
                _logger?.Log(_lpsRuntimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Failed To get dimensions.\n{ex.Message}\n{ex.InnerException?.Message}\n{ex.StackTrace}", LPSLoggingLevel.Error);
                return null;
            }
        }

        public static List<T> Get<T>(Func<T, bool> predicate) where T : IMetricMonitor
        {
            try
            {
                // Return all metric monitors of type T that match the predicate
                return _metrics.Values
                               .SelectMany(entry => entry.Item2.Values.OfType<T>()) // Flatten the collection of monitors and filter by type T
                               .Where(predicate) // Apply the predicate
                               .ToList();
            }
            catch (Exception ex)
            {
                _logger?.Log(_lpsRuntimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Failed To get dimensions.\n{ex.Message}\n{ex.InnerException?.Message}\n{ex.StackTrace}", LPSLoggingLevel.Error);
                return null;
            }
        }

    }
}
