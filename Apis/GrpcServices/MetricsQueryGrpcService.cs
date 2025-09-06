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
            var collectors = await FilterCollectorsAsync<DurationMetricAggregator, DurationMetricSnapshot>(request);

            var response = new DurationMetricSearchResponse();
            foreach (var collector in collectors)
            {
                var d = (DurationMetricSnapshot)await collector.GetSnapshotAsync(_cts.Token);
                response.Responses.Add(new DurationMetricResponse
                {
                    Metadata = BuildMetadata(d),
                    SumResponseTime = d.SumResponseTime,
                    AverageResponseTime = d.AverageResponseTime,
                    MinResponseTime = d.MinResponseTime,
                    MaxResponseTime = d.MaxResponseTime,
                    P90ResponseTime = d.P90ResponseTime,
                    P50ResponseTime = d.P50ResponseTime,
                    P10ResponseTime = d.P10ResponseTime
                });
            }

            return response;
        }

        public override async Task<DataTransmissionMetricSearchResponse> GetDataTransmissionMetrics(MetricRequest request, ServerCallContext context)
        {
            var collectors = await FilterCollectorsAsync<DataTransmissionMetricAggregator, DataTransmissionMetricSnapshot>(request);
            var response = new DataTransmissionMetricSearchResponse();

            foreach (var collector in collectors)
            {
                var d = (DataTransmissionMetricSnapshot)await collector.GetSnapshotAsync(_cts.Token);
                response.Responses.Add(new DataTransmissionMetricResponse
                {
                    Metadata = BuildMetadata(d),
                    TotalDataTransmissionTimeInMilliseconds = d.TotalDataTransmissionTimeInMilliseconds,
                    DataSent = d.DataSent,
                    DataReceived = d.DataReceived,
                    AverageDataSent = d.AverageDataSent,
                    AverageDataReceived = d.AverageDataReceived,
                    UpstreamThroughputBps = d.UpstreamThroughputBps,
                    DownstreamThroughputBps = d.DownstreamThroughputBps,
                    ThroughputBps = d.ThroughputBps
                });
            }

            return response;
        }

        public override async Task<ThroughputMetricSearchResponse> GetThroughputMetrics(MetricRequest request, ServerCallContext context)
        {
            var collectors = await FilterCollectorsAsync<ThroughputMetricAggregator, ThroughputMetricSnapshot>(request);
            var response = new ThroughputMetricSearchResponse();

            foreach (var collector in collectors)
            {
                var d = (ThroughputMetricSnapshot)await collector.GetSnapshotAsync(_cts.Token);
                response.Responses.Add(new ThroughputMetricResponse
                {
                    Metadata = BuildMetadata(d),
                    TotalDataTransmissionTimeInMilliseconds = d.TotalDataTransmissionTimeInMilliseconds,
                    RequestsCount = d.RequestsCount,
                    ActiveRequestsCount = d.ActiveRequestsCount,
                    SuccessfulRequestCount = d.SuccessfulRequestCount,
                    FailedRequestsCount = d.FailedRequestsCount,
                    ErrorRate = d.ErrorRate,
                    RequestsRate = new Protos.Shared.RequestsRate
                    {
                        Every = d.RequestsRate.Every ?? string.Empty,
                        Value = d.RequestsRate.Value
                    },
                    RequestsRatePerCoolDownPeriod = new Protos.Shared.RequestsRate
                    {
                        Every = d.RequestsRatePerCoolDownPeriod.Every ?? string.Empty,
                        Value = d.RequestsRatePerCoolDownPeriod.Value
                    }
                });
            }

            return response;

        }


        public override async Task<ResponseCodeMetricSearchResponse> GetResponseCodeMetrics(MetricRequest request, ServerCallContext context)
        {
            var collectors = await FilterCollectorsAsync<ResponseCodeMetricAggregator, ResponseCodeMetricSnapshot>(request);
            var response = new ResponseCodeMetricSearchResponse();

            foreach (var collector in collectors)
            {
                var d = (ResponseCodeMetricSnapshot)await collector.GetSnapshotAsync(_cts.Token);
                var resp = new ResponseCodeMetricResponse
                {
                    Metadata = BuildMetadata(d)
                };

                foreach (var summary in d.ResponseSummaries)
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

        private async Task<List<TCollector>> FilterCollectorsAsync<TCollector, TDimensionSet>(MetricRequest request)
            where TCollector : class, IMetricAggregator
            where TDimensionSet : HttpMetricSnapshot
        {
            var results = await _metricsQueryService.GetAsync<TCollector>(async collector =>
            {
                var d = (TDimensionSet)await collector.GetSnapshotAsync(_cts.Token);
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
