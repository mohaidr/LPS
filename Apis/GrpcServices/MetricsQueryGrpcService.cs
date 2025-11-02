using Grpc.Core;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Monitoring.MetricsServices;
using LPS.Protos.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.GRPCServices
{
    /// <summary>
    /// gRPC service that reads metrics from the in-memory IMetricDataStore (not from aggregators).
    /// It returns the latest snapshot per iteration/metric-type that matches the provided filters.
    /// </summary>
    public class MetricsQueryGrpcService : Protos.Shared.MetricsQueryService.MetricsQueryServiceBase
    {
        private readonly IMetricDataStore _metricDataStore;
        private readonly CancellationTokenSource _cts;

        public MetricsQueryGrpcService(IMetricDataStore metricDataStore, CancellationTokenSource cts)
        {
            _metricDataStore = metricDataStore ?? throw new ArgumentNullException(nameof(metricDataStore));
            _cts = cts ?? throw new ArgumentNullException(nameof(cts));
        }

        private static HttpMetricMetadata BuildMetadata(HttpMetricSnapshot s) => new()
        {
            RoundName = s.RoundName,
            IterationId = s.IterationId.ToString(),
            IterationName = s.IterationName,
            HttpMethod = s.HttpMethod,
            Url = s.URL,
            HttpVersion = s.HttpVersion
        };

        public override Task<DurationMetricSearchResponse> GetDurationMetrics(MetricRequest request, ServerCallContext context)
        {
            var snapshots = FilterLatestSnapshots<DurationMetricSnapshot>(request, MetricTypesFor<DurationMetricSnapshot>());
            var response = new DurationMetricSearchResponse();

            foreach (var s in snapshots)
            {
                response.Responses.Add(new DurationMetricResponse
                {
                    Metadata = BuildMetadata(s),
                    SumTotalTime = s.SumTotalTime,
                    AverageTotalTime = s.AverageTotalTime,
                    MinTotalTime = s.MinTotalTime,
                    MaxTotalTime = s.MaxTotalTime,
                    P90TotalTime = s.P90TotalTime,
                    P50TotalTime = s.P50TotalTime,
                    P10TotalTime = s.P10TotalTime
                });
            }

            return Task.FromResult(response);
        }

        public override Task<DataTransmissionMetricSearchResponse> GetDataTransmissionMetrics(MetricRequest request, ServerCallContext context)
        {
            var snapshots = FilterLatestSnapshots<DataTransmissionMetricSnapshot>(request, MetricTypesFor<DataTransmissionMetricSnapshot>());
            var response = new DataTransmissionMetricSearchResponse();

            foreach (var s in snapshots)
            {
                response.Responses.Add(new DataTransmissionMetricResponse
                {
                    Metadata = BuildMetadata(s),
                    TotalDataTransmissionTimeInMilliseconds = s.TotalDataTransmissionTimeInMilliseconds,
                    DataSent = s.DataSent,
                    DataReceived = s.DataReceived,
                    AverageDataSent = s.AverageDataSent,
                    AverageDataReceived = s.AverageDataReceived,
                    UpstreamThroughputBps = s.UpstreamThroughputBps,
                    DownstreamThroughputBps = s.DownstreamThroughputBps,
                    ThroughputBps = s.ThroughputBps
                });
            }

            return Task.FromResult(response);
        }

        public override Task<ThroughputMetricSearchResponse> GetThroughputMetrics(MetricRequest request, ServerCallContext context)
        {
            var snapshots = FilterLatestSnapshots<ThroughputMetricSnapshot>(request, MetricTypesFor<ThroughputMetricSnapshot>());
            var response = new ThroughputMetricSearchResponse();

            foreach (var s in snapshots)
            {
                response.Responses.Add(new ThroughputMetricResponse
                {
                    Metadata = BuildMetadata(s),
                    TotalDataTransmissionTimeInMilliseconds = s.TotalDataTransmissionTimeInMilliseconds,
                    RequestsCount = s.RequestsCount,
                    ActiveRequestsCount = s.ActiveRequestsCount,
                    SuccessfulRequestCount = s.SuccessfulRequestCount,
                    FailedRequestsCount = s.FailedRequestsCount,
                    ErrorRate = s.ErrorRate,
                    RequestsRate = new Protos.Shared.RequestsRate
                    {
                        Every = s.RequestsRate.Every ?? string.Empty,
                        Value = s.RequestsRate.Value
                    },
                    RequestsRatePerCoolDownPeriod = new Protos.Shared.RequestsRate
                    {
                        Every = s.RequestsRatePerCoolDownPeriod.Every ?? string.Empty,
                        Value = s.RequestsRatePerCoolDownPeriod.Value
                    }
                });
            }

            return Task.FromResult(response);
        }

        public override Task<ResponseCodeMetricSearchResponse> GetResponseCodeMetrics(MetricRequest request, ServerCallContext context)
        {
            var snapshots = FilterLatestSnapshots<ResponseCodeMetricSnapshot>(request, MetricTypesFor<ResponseCodeMetricSnapshot>());
            var response = new ResponseCodeMetricSearchResponse();

            foreach (var s in snapshots)
            {
                var resp = new ResponseCodeMetricResponse
                {
                    Metadata = BuildMetadata(s)
                };

                foreach (var summary in s.ResponseSummaries)
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

            return Task.FromResult(response);
        }

        /// <summary>
        /// Enumerate all iterations from the data store and return the latest snapshot per iteration
        /// for the given metric type(s) that matches the filters in the request.
        /// </summary>
        private List<TSnapshot> FilterLatestSnapshots<TSnapshot>(MetricRequest request, IEnumerable<LPSMetricType> metricTypes)
            where TSnapshot : HttpMetricSnapshot
        {
            var results = new List<TSnapshot>();

            foreach (var iteration in _metricDataStore.Iterations)
            {
                if (_cts.IsCancellationRequested) break;

                TSnapshot? snapshot = null;

                // Try all candidate metric types for this snapshot kind (handles enum naming differences like Duration/ResponseTime).
                foreach (var mt in metricTypes)
                {
                    if (_metricDataStore.TryGetLatest<TSnapshot>(iteration.Id, mt, out var s) && s != null)
                    {
                        snapshot = s;
                        break;
                    }
                }

                if (snapshot != null && Matches(request, snapshot))
                {
                    results.Add(snapshot);
                }
            }

            return results;
        }

        /// <summary>
        /// Map snapshot type to metric-type enums we should try in the store.
        /// This keeps things robust if your enum naming differs (e.g., Duration vs ResponseTime).
        /// </summary>
        private static IEnumerable<LPSMetricType> MetricTypesFor<TSnapshot>() where TSnapshot : HttpMetricSnapshot
        {
            if (typeof(TSnapshot) == typeof(ResponseCodeMetricSnapshot))
                return new[] { LPSMetricType.ResponseCode };

            if (typeof(TSnapshot) == typeof(ThroughputMetricSnapshot))
                return new[] { LPSMetricType.Throughput };

            if (typeof(TSnapshot) == typeof(DataTransmissionMetricSnapshot))
                return new[] { LPSMetricType.DataTransmission };

            if (typeof(TSnapshot) == typeof(DurationMetricSnapshot))
            {
                // Try both if your codebase sometimes uses ResponseTime
                var list = new List<LPSMetricType>();
                if (Enum.TryParse<LPSMetricType>("Duration", out var d)) list.Add(d);
                if (Enum.TryParse<LPSMetricType>("ResponseTime", out var rt)) list.Add(rt);
                return list;
            }

            // Fallback: try the enum name of the type (unlikely to help, but harmless)
            return Array.Empty<LPSMetricType>();
        }

        /// <summary>
        /// Apply the same filtering logic that was used against aggregator snapshots.
        /// </summary>
        private static bool Matches(MetricRequest request, HttpMetricSnapshot d)
        {
            var checks = new List<bool>();

            if (!string.IsNullOrEmpty(request.FullyQualifiedName))
                checks.Add(request.FullyQualifiedName.EndsWith($"/round/{d.RoundName}/iteration/{d.IterationName}", StringComparison.OrdinalIgnoreCase));

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

            if (checks.Count == 0)
                return request.Mode == FilterMode.Or ? false : true;

            return request.Mode == FilterMode.Or ? checks.Any(x => x) : checks.All(x => x);
        }
    }
}
