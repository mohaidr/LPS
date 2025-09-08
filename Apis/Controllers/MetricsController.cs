using LPS.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Status;
using LPS.Infrastructure.Monitoring.Metrics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using HdrHistogram;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;

namespace Apis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController(
        LPS.Domain.Common.Interfaces.ILogger logger,
        IIterationStatusMonitor iterationStatusMonitor,
        IRuntimeOperationIdProvider runtimeOperationIdProvider,
        IMetricsQueryService metricsQueryService, CancellationTokenSource cts) : ControllerBase
    {
        readonly LPS.Domain.Common.Interfaces.ILogger _logger = logger;
        readonly IRuntimeOperationIdProvider? _runtimeOperationIdProvider = runtimeOperationIdProvider;
        readonly IIterationStatusMonitor _iterationStatusMonitor = iterationStatusMonitor;
        readonly IMetricsQueryService _metricsQueryService = metricsQueryService;
        readonly CancellationTokenSource _cts = cts;
        // MetricData class extended to hold Data Transmission metrics
        private class MetricData
        {
            public DateTime TimeStamp { get; set; }
            public string URL { get; set; }
            public string HttpMethod { get; set; }
            public string HttpVersion { get; set; }
            public string RoundName { get; set; }
            public Guid IterationId { get; set; }
            public string IterationName { get; set; }
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
                var responseTimeMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.ResponseTime, _cts.Token);
                var responseBreakDownMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.ResponseCode, _cts.Token);
                var connectionsMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.Throughput, _cts.Token);
                var dataTransmissionMetricsTask = _metricsQueryService.GetAsync(metric => metric.MetricType == LPSMetricType.DataTransmission, _cts.Token);

                // Await all tasks to complete

                // Retrieve the results
                var responseTimeMetrics = await responseTimeMetricsTask;
                var responseBreakDownMetrics = await responseBreakDownMetricsTask;
                var connectionsMetrics = await connectionsMetricsTask;
                var dataTransmissionMetrics = await dataTransmissionMetricsTask; // Get data transmission metrics

                // Populate the metrics list
                await AddToList(responseTimeMetrics, "ResponseTime");
                await AddToList(responseBreakDownMetrics, "ResponseCode");
                await AddToList(connectionsMetrics, "ConnectionsCount");
                await AddToList(dataTransmissionMetrics, "DataTransmission"); // Add DataTransmission to the list
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
            async ValueTask AddToList(IEnumerable<dynamic> metrics, string type)
            {
                foreach (var metric in metrics)
                {
                    var dimensionSet = await ((IMetricAggregator)metric).GetSnapshotAsync(_cts.Token);

                    string status = (await _iterationStatusMonitor.GetTerminalStatusAsync(((IMetricAggregator)metric).HttpIteration)).ToString();

                    var metricData = metricsList.FirstOrDefault(m => m.IterationId == ((IHttpSnapshot)dimensionSet).IterationId);
                    if (metricData == null)
                    {
                        metricData = new MetricData
                        {
                            ExecutionStatus = status,
                            TimeStamp = ((IHttpSnapshot)dimensionSet).TimeStamp,
                            RoundName = ((IHttpSnapshot)dimensionSet).RoundName,
                            IterationId = ((IHttpSnapshot)dimensionSet).IterationId,
                            IterationName = ((IHttpSnapshot)dimensionSet).IterationName,
                            URL = ((IHttpSnapshot)dimensionSet).URL,
                            HttpMethod = ((IHttpSnapshot)dimensionSet).HttpMethod,
                            HttpVersion = ((IHttpSnapshot)dimensionSet).HttpVersion,
                            Endpoint = $"{((IHttpSnapshot)dimensionSet).IterationName} {((IHttpSnapshot)dimensionSet).URL} HTTP/{((IHttpSnapshot)dimensionSet).HttpVersion}"
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
                            metricData.ResponseTimeMetrics = await ((IMetricAggregator)metric).GetSnapshotAsync(_cts.Token);
                            break;
                        case "ResponseCode":
                            metricData.ResponseBreakDownMetrics = await ((IMetricAggregator)metric).GetSnapshotAsync(_cts.Token);
                            break;
                        case "ConnectionsCount":
                            metricData.ConnectionMetrics = await ((IMetricAggregator)metric).GetSnapshotAsync(_cts.Token);
                            break;
                        case "DataTransmission":  // Handle data transmission metrics
                            metricData.DataTransmissionMetrics = await ((IMetricAggregator)metric).GetSnapshotAsync(_cts.Token);
                            break;
                    }
                }
            }
        }
    }
}
