using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Command;
using LPS.Infrastructure.Monitoring.Metrics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Sockets;


namespace LPS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController: ControllerBase
    {
        private static bool _isServerRunning = false;
        private static readonly object _lockObject = new object();
        private static ILPSLogger _logger;
        private static ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private static ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> _httpRunCommandStatusMonitor;
        static CancellationToken _testCancellationToken;

        public MetricsController(ILPSLogger logger, ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> httpRunCommandStatusMonitor, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider, CancellationTokenSource cts)
        {
            _logger = logger;
            _httpRunCommandStatusMonitor = httpRunCommandStatusMonitor;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _testCancellationToken = cts.Token;
        }

        private class MetricData
        {
            public string Endpoint { get; set; }
            public string ExecutionStatus { get; set; }
            public object ResponseBreakDownMetrics { get; set; }
            public object ResponseTimeMetrics { get; set; }
            public object ConnectionMetrics { get; set; }
        }

        [HttpGet]
        public IActionResult Get()
        {
            var metricsList = new List<MetricData>();

            // Define a local function to safely obtain endpoint details from a metric dimension set
            string? GetEndPointDetails(IDimensionSet dimensionSet)
            {
                if (dimensionSet is LPSDurationMetricDimensionSet durationSet)
                    return $"{durationSet.RunName} {durationSet.HttpMethod} {durationSet.URL} HTTP/{durationSet.HttpVersion}";
                else if (dimensionSet is ResponseCodeDimensionSet responseSet)
                    return $"{responseSet.RunName} {responseSet.HttpMethod} {responseSet.URL} HTTP/{responseSet.HttpVersion}";
                else if (dimensionSet is ConnectionDimensionSet connectionSet)
                    return $"{connectionSet.RunName} {connectionSet.HttpMethod} {connectionSet.URL} HTTP/{connectionSet.HttpVersion}";

                return null; // or a default string if suitable
            }

            // Helper action to add metrics to the list
            Action<IEnumerable<dynamic>, string> addToList = (metrics, type) =>
            {
                foreach (var metric in metrics)
                {
                    var endPointDetails = GetEndPointDetails(metric.GetDimensionSet());
                    if (endPointDetails == null)
                        continue; // Skip metrics where endpoint details are not applicable or available

                    var statusList = _httpRunCommandStatusMonitor.GetAllStatuses(((ILPSMetricMonitor)metric).LPSHttpRun);
                    string status = DetermineOverallStatus(statusList);


                    var metricData = metricsList.FirstOrDefault(m => m.Endpoint == endPointDetails);
                    if (metricData == null)
                    {
                        metricData = new MetricData
                        {
                            ExecutionStatus = status,
                            Endpoint = endPointDetails
                        };
                        metricsList.Add(metricData);
                    }
                    else
                    if (metricData.ExecutionStatus != status)
                    {
                        metricData.ExecutionStatus = status;
                    }

                    switch (type)
                    {
                        case "ResponseTime":
                            metricData.ResponseTimeMetrics = metric.GetDimensionSet();
                            break;
                        case "ResponseCode":
                            metricData.ResponseBreakDownMetrics = metric.GetDimensionSet();
                            break;
                        case "ConnectionsCount":
                            metricData.ConnectionMetrics = metric.GetDimensionSet();
                            break;
                    }
                }
            };
            // Fetch metrics by type
            var responseTimeMetrics = LPSMetricsDataMonitor.Get(metric => metric.MetricType == LPSMetricType.ResponseTime);
            var responseBreakDownMetrics = LPSMetricsDataMonitor.Get(metric => metric.MetricType == LPSMetricType.ResponseCode);
            var connectionsMetrics = LPSMetricsDataMonitor.Get(metric => metric.MetricType == LPSMetricType.ConnectionsCount);

            // Populate the dictionary
            addToList(responseTimeMetrics, "ResponseTime");
            addToList(responseBreakDownMetrics, "ResponseCode");
            addToList(connectionsMetrics, "ConnectionsCount");

            return Ok(metricsList);
        }

        private static string DetermineOverallStatus(List<AsyncCommandStatus> statuses)
        {
            if (statuses.Count == 0)
                return "NotRunning";
            if (statuses.All(status => status == AsyncCommandStatus.NotStarted))
                return "NotStarted";
            if (statuses.All(status => status == AsyncCommandStatus.Completed))
                return "Completed";
            if (statuses.All(status => status == AsyncCommandStatus.Completed || status == AsyncCommandStatus.Paused) && statuses.Any(status => status == AsyncCommandStatus.Paused))
                return "Paused";
            if (statuses.All(status => status == AsyncCommandStatus.Completed || status == AsyncCommandStatus.Failed) && statuses.Any(status => status == AsyncCommandStatus.Failed))
                return "Failed";
            if (statuses.All(status => status == AsyncCommandStatus.Completed || status == AsyncCommandStatus.Cancelled) && statuses.Any(status => status == AsyncCommandStatus.Cancelled))
                return "Cancelled";
            if (statuses.Any(status => status == AsyncCommandStatus.Ongoing))
                return "Ongoing";
            return "Undefined"; // Default case, should ideally never be reached
        }
    }
}
