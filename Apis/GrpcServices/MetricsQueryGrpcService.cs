using Grpc.Core;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Protos.Shared;
using System.Linq;
using System.Threading.Tasks;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Infrastructure.Nodes;
using System;
using System.Collections.Generic;

namespace LPS.Infrastructure.Monitoring.GRPCServices
{
    public class MetricsQueryGrpcService : Protos.Shared.MetricsQueryService.MetricsQueryServiceBase
    {
        private readonly IMetricsQueryService _metricsQueryService;
        CancellationTokenSource _cts;
        public MetricsQueryGrpcService(IMetricsQueryService metricsQueryService, CancellationTokenSource cts)
        {
            _metricsQueryService = metricsQueryService;
            _cts = cts;
        }

        private HttpMetricMetadata BuildMetadata(HttpMetricSnapshot dimension) => new()
        {
            RoundName = dimension.RoundName,
            IterationId = dimension.IterationId.ToString(),
            IterationName = dimension.IterationName,
            HttpMethod = dimension.HttpMethod,
            Url = dimension.URL,
            HttpVersion = dimension.HttpVersion
        };

        public override async Task<DurationMetricSearchResponse> GetDurationMetrics(MetricRequest request, ServerCallContext context)
        {
            var aggregators = await FilterAggregatorsAsync<DurationMetricAggregator, DurationMetricSnapshot>(request);

            var response = new DurationMetricSearchResponse();
            foreach (var aggregator in aggregators)
            {
                var snapshot = (DurationMetricSnapshot)await aggregator.GetSnapshotAsync(_cts.Token);
                response.Responses.Add(new DurationMetricResponse
                {
                    Metadata = BuildMetadata(snapshot),
                    SumResponseTime = snapshot.SumResponseTime,
                    AverageResponseTime = snapshot.AverageResponseTime,
                    MinResponseTime = snapshot.MinResponseTime,
                    MaxResponseTime = snapshot.MaxResponseTime,
                    P90ResponseTime = snapshot.P90ResponseTime,
                    P50ResponseTime = snapshot.P50ResponseTime,
                    P10ResponseTime = snapshot.P10ResponseTime
                });
            }

            return response;
        }

        public override async Task<DataTransmissionMetricSearchResponse> GetDataTransmissionMetrics(MetricRequest request, ServerCallContext context)
        {
            var aggregators = await FilterAggregatorsAsync<DataTransmissionMetricAggregator, DataTransmissionMetricSnapshot>(request);
            var response = new DataTransmissionMetricSearchResponse();

            foreach (var aggregator in aggregators)
            {
                var snapshot = (DataTransmissionMetricSnapshot)await aggregator.GetSnapshotAsync(_cts.Token);
                response.Responses.Add(new DataTransmissionMetricResponse
                {
                    Metadata = BuildMetadata(snapshot),
                    TotalDataTransmissionTimeInMilliseconds = snapshot.TotalDataTransmissionTimeInMilliseconds,
                    DataSent = snapshot.DataSent,
                    DataReceived = snapshot.DataReceived,
                    AverageDataSent = snapshot.AverageDataSent,
                    AverageDataReceived = snapshot.AverageDataReceived,
                    UpstreamThroughputBps = snapshot.UpstreamThroughputBps,
                    DownstreamThroughputBps = snapshot.DownstreamThroughputBps,
                    ThroughputBps = snapshot.ThroughputBps
                });
            }

            return response;
        }

        public override async Task<ThroughputMetricSearchResponse> GetThroughputMetrics(MetricRequest request, ServerCallContext context)
        {
            var aggregators = await FilterAggregatorsAsync<ThroughputMetricAggregator, ThroughputMetricSnapshot>(request);
            var response = new ThroughputMetricSearchResponse();

            foreach (var aggregator in aggregators)
            {
                var snapshot = (ThroughputMetricSnapshot)await aggregator.GetSnapshotAsync(_cts.Token);
                response.Responses.Add(new ThroughputMetricResponse
                {
                    Metadata = BuildMetadata(snapshot),
                    TotalDataTransmissionTimeInMilliseconds = snapshot.TotalDataTransmissionTimeInMilliseconds,
                    RequestsCount = snapshot.RequestsCount,
                    ActiveRequestsCount = snapshot.ActiveRequestsCount,
                    SuccessfulRequestCount = snapshot.SuccessfulRequestCount,
                    FailedRequestsCount = snapshot.FailedRequestsCount,
                    ErrorRate = snapshot.ErrorRate,
                    RequestsRate = new Protos.Shared.RequestsRate
                    {
                        Every = snapshot.RequestsRate.Every ?? string.Empty,
                        Value = snapshot.RequestsRate.Value
                    },
                    RequestsRatePerCoolDownPeriod = new Protos.Shared.RequestsRate
                    {
                        Every = snapshot.RequestsRatePerCoolDownPeriod.Every ?? string.Empty,
                        Value = snapshot.RequestsRatePerCoolDownPeriod.Value
                    }
                });
            }

            return response;

        }


        public override async Task<ResponseCodeMetricSearchResponse> GetResponseCodeMetrics(MetricRequest request, ServerCallContext context)
        {
            var aggregators = await FilterAggregatorsAsync<ResponseCodeMetricAggregator, ResponseCodeMetricSnapshot>(request);
            var response = new ResponseCodeMetricSearchResponse();

            foreach (var aggregator in aggregators)
            {
                var snapshot = (ResponseCodeMetricSnapshot)await aggregator.GetSnapshotAsync(_cts.Token);
                var resp = new ResponseCodeMetricResponse
                {
                    Metadata = BuildMetadata(snapshot)
                };

                foreach (var summary in snapshot.ResponseSummaries)
                {
                    resp.Summaries.Add(new Protos.Shared.HttpResponseSummary
                    {
                        HttpStatusCode = summary.HttpStatusCode.ToString(),
                        HttpStatusReason = summary.HttpStatusReason,
                        Count = summary.Count
                    });
                }

                response.Responses.Add(resp);
            }

            return response;
        }

        private async Task<List<TAggregator>> FilterAggregatorsAsync<TAggregator, TSnapshot>(MetricRequest request)
            where TAggregator : class, IMetricAggregator
            where TSnapshot : HttpMetricSnapshot
        {
            var results = await _metricsQueryService.GetAsync<TAggregator>(async collector =>
            {
                var d = (TSnapshot)await collector.GetSnapshotAsync(_cts.Token);
                var checks = new List<bool>();

                if (!string.IsNullOrEmpty(request.FullyQualifiedName))
                {
                    checks.Add(request.FullyQualifiedName.EndsWith($"/round/{d.RoundName}/iteration/{d.IterationName}", StringComparison.OrdinalIgnoreCase));
                }


                if (!string.IsNullOrEmpty(request.RoundName))
                    checks.Add(d.RoundName == request.RoundName);

                if (!string.IsNullOrEmpty(request.IterationId))
                    checks.Add(d.IterationId.ToString() == request.IterationId);

                if (!string.IsNullOrEmpty(request.IterationName))
                    checks.Add(d.IterationName == request.IterationName);

                if (!string.IsNullOrEmpty(request.HttpMethod))
                    checks.Add(d.HttpMethod == request.HttpMethod);

                if (!string.IsNullOrEmpty(request.HttpVersion))
                    checks.Add(d.HttpVersion == request.HttpVersion);

                if (!string.IsNullOrEmpty(request.Hostname) && Uri.TryCreate(d.URL, UriKind.Absolute, out var uriHost))
                    checks.Add(uriHost.Host == request.Hostname);

                if (!string.IsNullOrEmpty(request.Path) && Uri.TryCreate(d.URL, UriKind.Absolute, out var uriPath))
                    checks.Add(uriPath.AbsolutePath == request.Path);

                return request.Mode == FilterMode.Or
                    ? checks.Any(x => x)
                    : checks.All(x => x);
            }, _cts.Token);

            return results;

        }
    }
}
