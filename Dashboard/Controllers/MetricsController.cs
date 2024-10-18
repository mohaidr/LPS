using LPS.Domain.Common.Interfaces;
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
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;

namespace LPS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController(
        Domain.Common.Interfaces.ILogger logger,
        ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> httpRunCommandStatusMonitor,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMetricsQueryService metricsQueryService) : ControllerBase
    {
        readonly Domain.Common.Interfaces.ILogger _logger = logger;
        readonly IRuntimeOperationIdProvider? _runtimeOperationIdProvider = runtimeOperationIdProvider;
        readonly ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun>? _httpRunCommandStatusMonitor = httpRunCommandStatusMonitor;
        readonly IMetricsQueryService _metricsQueryService = metricsQueryService;

        // MetricData class extended to hold Data Transmission metrics
        private class MetricData
        {
            public string Endpoint { get; set; }
            public string ExecutionStatus { get; set; }
            public object ResponseBreakDownMetrics { get; set; }
            public object ResponseTimeMetrics { get; set; }
            public object ConnectionMetrics { get; set; }
            public object DataTransmissionMetrics { get; set; } 
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var metricsList = new List<MetricData>();

            try
            {
                // Initiate asynchronous fetching of metrics
                var responseTimeMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.ResponseTime);
                var responseBreakDownMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.ResponseCode);
                var connectionsMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.Throughput);
                var dataTransmissionMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.DataTransmission);

                // Await all tasks to complete
                await Task.WhenAll(responseTimeMetricsTask, responseBreakDownMetricsTask, connectionsMetricsTask, dataTransmissionMetricsTask);

                // Retrieve the results
                var responseTimeMetrics = responseTimeMetricsTask.Result;
                var responseBreakDownMetrics = responseBreakDownMetricsTask.Result;
                var connectionsMetrics = connectionsMetricsTask.Result;
                var dataTransmissionMetrics = dataTransmissionMetricsTask.Result; // Get data transmission metrics

                // Populate the metrics list
                AddToList(responseTimeMetrics, "ResponseTime");
                AddToList(responseBreakDownMetrics, "ResponseCode");
                AddToList(connectionsMetrics, "ConnectionsCount");
                AddToList(dataTransmissionMetrics, "DataTransmission"); // Add DataTransmission to the list
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

            // Helper action to add metrics to the list
            async void AddToList(IEnumerable<dynamic> metrics, string type)
            {
                foreach (var metric in metrics)
                {
                    var dimensionSet = await ((IMetricCollector)metric).GetDimensionSetAsync();
                    var endPointDetails = GetEndPointDetails(dimensionSet);
                    if (endPointDetails == null)
                        continue; // Skip metrics where endpoint details are not applicable or available

                    var statusList = _httpRunCommandStatusMonitor?.GetAllStatuses(((IMetricCollector)metric).LPSHttpRun);
                    string status = statusList != null ? DetermineOverallStatus(statusList) : ExecutionStatus.Unkown.ToString();

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
                            metricData.ResponseTimeMetrics = await ((IMetricCollector)metric).GetDimensionSetAsync();
                            break;
                        case "ResponseCode":
                            metricData.ResponseBreakDownMetrics = await ((IMetricCollector)metric).GetDimensionSetAsync();
                            break;
                        case "ConnectionsCount":
                            metricData.ConnectionMetrics = await ((IMetricCollector)metric).GetDimensionSetAsync();
                            break;
                        case "DataTransmission":  // Handle data transmission metrics
                            metricData.DataTransmissionMetrics = await ((IMetricCollector)metric).GetDimensionSetAsync();
                            break;
                    }
                }
            }

            // Define a local function to safely obtain endpoint details from a metric dimension set
            string? GetEndPointDetails(IDimensionSet dimensionSet)
            {
                return dimensionSet switch
                {
                    LPSDurationMetricDimensionSet durationSet => $"{durationSet.RunName} {durationSet.HttpMethod} {durationSet.URL} HTTP/{durationSet.HttpVersion}",
                    ResponseCodeDimensionSet responseSet => $"{responseSet.RunName} {responseSet.HttpMethod} {responseSet.URL} HTTP/{responseSet.HttpVersion}",
                    ThroughputDimensionSet connectionSet => $"{connectionSet.RunName} {connectionSet.HttpMethod} {connectionSet.URL} HTTP/{connectionSet.HttpVersion}",
                    LPSDataTransmissionMetricDimensionSet dataTransmissionSet => $"{dataTransmissionSet.RunName} {dataTransmissionSet.HttpMethod} {dataTransmissionSet.URL} HTTP/{dataTransmissionSet.HttpVersion}",  // New case for DataTransmission
                    _ => null // or a default string if suitable
                };
            }
        }

        private static string DetermineOverallStatus(List<ExecutionStatus> statuses)
        {
            if (statuses.Count == 0 || statuses.All(status => status == ExecutionStatus.PendingExecution))
                return "PendingExecution";
            if (statuses.Any(status => status == ExecutionStatus.Scheduled) && !statuses.Any(status => status == ExecutionStatus.Ongoing))
                return "Scheduled";
            if (statuses.Any(status => status == ExecutionStatus.Ongoing))
                return "Ongoing";
            if (statuses.Any(status => status == ExecutionStatus.Failed))
                return "Failed";
            if (statuses.Any(status => status == ExecutionStatus.Cancelled) && !statuses.Any(status => status == ExecutionStatus.Failed))
                return "Cancelled";
            if (statuses.All(status => status == ExecutionStatus.Completed))
                return "Completed";
            if (statuses.All(status => status == ExecutionStatus.Completed || status == ExecutionStatus.Paused) && statuses.Any(status => status == ExecutionStatus.Paused))
                return "Paused";
            return "Undefined"; // Default case, should ideally never be reached
        }
    }
}
