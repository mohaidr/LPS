using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Command;
using LPS.Infrastructure.Monitoring.Metrics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private static bool _isServerRunning = false;
        private static readonly object _lockObject = new object();
        private static Domain.Common.Interfaces.ILogger _logger;
        private static IRuntimeOperationIdProvider? _runtimeOperationIdProvider;
        private static ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun>? _httpRunCommandStatusMonitor;
        private static CancellationToken _testCancellationToken;

        public MetricsController(
            Domain.Common.Interfaces.ILogger logger,
            ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunCommandStatusMonitor,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            CancellationTokenSource cts)
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
        public async Task<IActionResult> Get()
        {
            var metricsList = new List<MetricData>();

            // Define a local function to safely obtain endpoint details from a metric dimension set
            string? GetEndPointDetails(IDimensionSet dimensionSet)
            {
                return dimensionSet switch
                {
                    LPSDurationMetricDimensionSet durationSet => $"{durationSet.RunName} {durationSet.HttpMethod} {durationSet.URL} HTTP/{durationSet.HttpVersion}",
                    ResponseCodeDimensionSet responseSet => $"{responseSet.RunName} {responseSet.HttpMethod} {responseSet.URL} HTTP/{responseSet.HttpVersion}",
                    ThroughputDimensionSet connectionSet => $"{connectionSet.RunName} {connectionSet.HttpMethod} {connectionSet.URL} HTTP/{connectionSet.HttpVersion}",
                    _ => null // or a default string if suitable
                };
            }

            // Helper action to add metrics to the list
            async void AddToList(IEnumerable<dynamic> metrics, string type)
            {
                foreach (var metric in metrics)
                {
                    var dimensionSet = await ((IMetricMonitor)metric).GetDimensionSetAsync();
                    var endPointDetails = GetEndPointDetails(dimensionSet);
                    if (endPointDetails == null)
                        continue; // Skip metrics where endpoint details are not applicable or available

                    var statusList = _httpRunCommandStatusMonitor?.GetAllStatuses(((IMetricMonitor)metric).LPSHttpRun);
                    string status = statusList != null ? DetermineOverallStatus(statusList) : AsyncCommandStatus.Unkown.ToString();

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
                    else if (metricData.ExecutionStatus != status)
                    {
                        metricData.ExecutionStatus = status;
                    }

                    switch (type)
                    {
                        case "ResponseTime":
                            metricData.ResponseTimeMetrics = await ((IMetricMonitor)metric).GetDimensionSetAsync(); ;
                            break;
                        case "ResponseCode":
                            metricData.ResponseBreakDownMetrics = await ((IMetricMonitor)metric).GetDimensionSetAsync(); ;
                            break;
                        case "ConnectionsCount":
                            metricData.ConnectionMetrics = await ((IMetricMonitor)metric).GetDimensionSetAsync(); ;
                            break;
                    }
                }
            }

            try
            {
                // Initiate asynchronous fetching of metrics
                var responseTimeMetricsTask = MetricsDataMonitor.GetAsync(metric => metric.MetricType == LPSMetricType.ResponseTime);
                var responseBreakDownMetricsTask = MetricsDataMonitor.GetAsync(metric => metric.MetricType == LPSMetricType.ResponseCode);
                var connectionsMetricsTask = MetricsDataMonitor.GetAsync(metric => metric.MetricType == LPSMetricType.ConnectionsCount);

                // Await all tasks to complete
                await Task.WhenAll(responseTimeMetricsTask, responseBreakDownMetricsTask, connectionsMetricsTask);

                // Retrieve the results
                var responseTimeMetrics = responseTimeMetricsTask.Result;
                var responseBreakDownMetrics = responseBreakDownMetricsTask.Result;
                var connectionsMetrics = connectionsMetricsTask.Result;

                // Populate the metrics list
                AddToList(responseTimeMetrics, "ResponseTime");
                AddToList(responseBreakDownMetrics, "ResponseCode");
                AddToList(connectionsMetrics, "ConnectionsCount");
            }
            catch (Exception ex)
            {
                // Log the exception asynchronously
                if (_logger != null)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider?.OperationId ?? "0000-0000-0000-0000",
                        $"Failed to retrieve metrics.\n{ex.Message}\n{ex.InnerException?.Message}\n{ex.StackTrace}",
                        LPSLoggingLevel.Error);
                }

                return StatusCode(500, "An error occurred while retrieving metrics.");
            }

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
