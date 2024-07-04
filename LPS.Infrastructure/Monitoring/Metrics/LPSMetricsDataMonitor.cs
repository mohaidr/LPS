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
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{
    public class LPSMetricsDataMonitor : ILPSMetricsDataMonitor
    {
        private static ILPSLogger _logger;
        private static ILPSRuntimeOperationIdProvider _lpsRuntimeOperationIdProvider;

        // ConcurrentDictionary to manage both metrics and execution lists with multiple monitors per LPSHttpRun
        private static ConcurrentDictionary<LPSHttpRun, Tuple<IList<string>, Dictionary<string, ILPSMetricMonitor>>> _metrics = new ConcurrentDictionary<LPSHttpRun, Tuple<IList<string>, Dictionary<string, ILPSMetricMonitor>>>();

        public LPSMetricsDataMonitor(ILPSLogger logger, ILPSRuntimeOperationIdProvider lpsRuntimeOperationIdProvider)
        {
            _logger = logger;
            _lpsRuntimeOperationIdProvider = lpsRuntimeOperationIdProvider;
        }

        public bool TryRegister(LPSHttpRun lpsHttpRun)
        {
            // Check if the key already exists
            if (_metrics.ContainsKey(lpsHttpRun))
            {
                // If it exists, return false indicating the registration did not proceed
                return false;
            }

            // Prepare the metrics dictionary for the given LPSHttpRun
            var metrics = new Dictionary<string, ILPSMetricMonitor>();
            string breakDownMetricKey = $"{lpsHttpRun.Id}-BreakDown";
            string durationMetricKey = $"{lpsHttpRun.Id}-Duration";
            string connectionsMetricKey = $"{lpsHttpRun.Id}-Connections";

            metrics[breakDownMetricKey] = new LPSResponseCodeMetricMonitor(lpsHttpRun, _logger, _lpsRuntimeOperationIdProvider);
            metrics[durationMetricKey] = new LPSDurationMetricMonitor(lpsHttpRun, _logger, _lpsRuntimeOperationIdProvider);
            metrics[connectionsMetricKey] = new LPSConnectionsMetricMonitor(lpsHttpRun, _logger, _lpsRuntimeOperationIdProvider);

            // Prepare the tuple to be added
            var metricsTuple = new Tuple<IList<string>, Dictionary<string, ILPSMetricMonitor>>(new List<string>() { }, metrics);
            return _metrics.TryAdd(lpsHttpRun, metricsTuple);
           // Try to add the new entry to the dictionary
        }

        public void Monitor(LPSHttpRun lpsHttpRun, string executionId)
        {
            try
            {
                var tuple = _metrics.GetOrAdd(lpsHttpRun, run =>
                {
                    var metrics = new Dictionary<string, ILPSMetricMonitor>();
                    // Create and start all necessary monitors with unique IDs
                    string breakDownMetricKey = $"{run.Id}-BreakDown";
                    string durationMetricKey = $"{run.Id}-Duration";
                    string connectionsMetricKey = $"{run.Id}-Connections";

                    metrics[breakDownMetricKey] = new LPSResponseCodeMetricMonitor(run, _logger, _lpsRuntimeOperationIdProvider);
                    metrics[durationMetricKey] = new LPSDurationMetricMonitor(run, _logger, _lpsRuntimeOperationIdProvider);
                    metrics[connectionsMetricKey] = new LPSConnectionsMetricMonitor(run, _logger, _lpsRuntimeOperationIdProvider);

                    return new Tuple<IList<string>, Dictionary<string, ILPSMetricMonitor>>(new List<string>(), metrics);
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

        public void Stop(LPSHttpRun lpsHttpRun, string executionId)
        {
            if (_metrics.TryGetValue(lpsHttpRun, out Tuple<IList<string>, Dictionary<string, ILPSMetricMonitor>> tuple))
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

        public static List<ILPSMetricMonitor> Get(Func<ILPSMetricMonitor, bool> predicate)
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

        public static List<T> Get<T>(Func<T, bool> predicate) where T : ILPSMetricMonitor
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
