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
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.Metrics
{

    public static class LPSMetricsDataSource
    {
        private static ConcurrentDictionary<string, ILPSMetric> _metrics = new ConcurrentDictionary<string, ILPSMetric>();

        private static ILPSLogger _logger;
        private static ILPSRuntimeOperationIdProvider _lpsRuntimeOperationIdProvider;
        internal static void Register(LPSHttpRun lpsHttpRun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider lpsRuntimeOperationIdProvider = default)
        {
            _logger = logger;
            _lpsRuntimeOperationIdProvider = lpsRuntimeOperationIdProvider;
            string breakDownMetricKey = $"{lpsHttpRun.Id}-BreakDown";
            string durationMetricKey = $"{lpsHttpRun.Id}-Duration";
            string connectionsMetricKey = $"{lpsHttpRun.Id}-Connections";

            bool isBreakDownMetricRegistered = _metrics.TryAdd(breakDownMetricKey, new LPSResponseCodeMetricGroup(lpsHttpRun, logger, lpsRuntimeOperationIdProvider));
            bool isDurationMetricRegistered = _metrics.TryAdd(durationMetricKey, new LPSDurationMetric(lpsHttpRun, logger, lpsRuntimeOperationIdProvider));
            bool isConnectionsMetricRegistered = _metrics.TryAdd(connectionsMetricKey, new LPSConnectionsMetricGroup(lpsHttpRun, logger, lpsRuntimeOperationIdProvider));

            logger?.Log(lpsRuntimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Host: {new Uri(lpsHttpRun.LPSHttpRequestProfile.URL).Host}, Metric Id: {breakDownMetricKey} Registered: {isBreakDownMetricRegistered}, Metric Id: {durationMetricKey} Registered: {isDurationMetricRegistered}, Metric Id: {connectionsMetricKey} Is Registered: {isConnectionsMetricRegistered}", LPSLoggingLevel.Verbos);
        }

        internal static void Deregister(LPSHttpRun lpsHttpRun, ILPSLogger logger = default, ILPSRuntimeOperationIdProvider lpsRuntimeOperationIdProvider = default)
        {
            _logger = logger;
            _lpsRuntimeOperationIdProvider = lpsRuntimeOperationIdProvider;
            string breakDownMetricKey = $"{lpsHttpRun.Id}-BreakDown";
            string durationMetricKey = $"{lpsHttpRun.Id}-Duration";
            string connectionsMetricKey = $"{lpsHttpRun.Id}-Connections";

            // Attempt to remove the metrics from the dictionary and dispose them
            if (_metrics.TryGetValue(breakDownMetricKey, out ILPSMetric removedBreakDownMetric) && removedBreakDownMetric is IDisposable disposableBreakDown)
            {
                disposableBreakDown.Dispose();
            }
            if (_metrics.TryGetValue(durationMetricKey, out ILPSMetric removedDurationMetric) && removedDurationMetric is IDisposable disposableDuration)
            {
                disposableDuration.Dispose();
            }
            if (_metrics.TryGetValue(connectionsMetricKey, out ILPSMetric removedConnectionsMetric) && removedConnectionsMetric is IDisposable disposableConnections)
            {
                disposableConnections.Dispose();
            }
            logger?.Log(lpsRuntimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Host: {new Uri(lpsHttpRun.LPSHttpRequestProfile.URL).Host}, Metric Id: {breakDownMetricKey} Deregistered: {removedBreakDownMetric != null},  Metric Id: {durationMetricKey} Deregistered: {removedDurationMetric != null},  Metric Id: {connectionsMetricKey} Deregistered: {removedConnectionsMetric != null}", LPSLoggingLevel.Verbos);
        }
        public static List<ILPSMetric> Get(Func<ILPSMetric, bool> predicate)
        {
            try
            {
                // Filter the metrics based on the given predicate.
                return _metrics.Values.Where(predicate).ToList();
            }
            catch (Exception ex)
            {
                _logger?.Log(_lpsRuntimeOperationIdProvider.OperationId ?? "0000-0000-0000-0000", $"Failed To get dimensions.\n{ex.Message}\n{ex.InnerException?.Message}\n{ex.StackTrace}", LPSLoggingLevel.Error);
                return null;
            }
        }

        public static List<T> Get<T>(Func<T, bool> predicate) where T : ILPSMetric
        {

            try
            {

                // Filter the metrics based on the given predicate and the type T.
                return _metrics.Values
                               .OfType<T>() // Ensure the metric is of type T
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
