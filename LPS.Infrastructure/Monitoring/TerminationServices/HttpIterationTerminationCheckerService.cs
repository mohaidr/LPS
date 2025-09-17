// HttpIterationTerminationCheckerService.cs (updated)
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Monitoring.TerminationServices
{
    internal enum TerminationMetricKind
    {
        ErrorRate,
        P90,
        P50,
        P10,
        Average
    }

    public class HttpIterationTerminationCheckerService : ITerminationCheckerService
    {
        // Track grace state per (iteration, rule, metric kind)
        private readonly ConcurrentDictionary<(Guid, TerminationRule, TerminationMetricKind), GracePeriodState> _state = new();

        private readonly ConcurrentDictionary<Guid, bool> _terminatedIterations = new();

        private readonly IEntityDiscoveryService _discoveryService;
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly IClusterConfiguration _clusterConfig;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        private readonly GrpcIterationTerminationServiceClient _grpcTermClient;

        public HttpIterationTerminationCheckerService(
            IEntityDiscoveryService discoveryService,
            ICustomGrpcClientFactory grpcClientFactory,
            IClusterConfiguration clusterConfig,
            INodeMetadata nodeMetadata,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _discoveryService = discoveryService;
            _grpcClientFactory = grpcClientFactory;
            _clusterConfig = clusterConfig;
            _nodeMetadata = nodeMetadata;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;

            _grpcTermClient = _grpcClientFactory.GetClient<GrpcIterationTerminationServiceClient>(_clusterConfig.MasterNodeIP);
        }

        public async Task<bool> IsTerminationRequiredAsync(Iteration iteration, CancellationToken token = default)
        {
            try
            {
                if (iteration is not HttpIteration httpIteration)
                    throw new ArgumentException("Expected HttpIteration", nameof(iteration));

                if (_terminatedIterations.ContainsKey(httpIteration.Id))
                    return true;

                if (httpIteration.TerminationRules == null || !httpIteration.TerminationRules.Any())
                    return false;

                var fqdn = _discoveryService
                    .Discover(r => r.IterationId == httpIteration.Id)
                    .FirstOrDefault()?.FullyQualifiedName;

                if (_nodeMetadata.NodeType == Nodes.NodeType.Worker)
                {
                    if (fqdn == null)
                        throw new ArgumentException(nameof(fqdn));

                    return await _grpcTermClient.IsTerminatedAsync(fqdn);
                }

                // Master: fetch metrics we need (response codes for error rate, duration for percentiles/avg)
                var metricsClient = _grpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfig.MasterNodeIP);

                // Response codes: used for error rate rule
                var respCodes = await metricsClient.GetResponseCodesAsync(fqdn, token);
                var respSummaries = respCodes?.Responses?.SingleOrDefault()?.Summaries;

                // Duration stats: used for p90/p50/p10/avg rules
                var durations = await metricsClient.GetDurationAsync(fqdn, token);
                var duration = durations?.Responses?.SingleOrDefault();

                foreach (var rule in httpIteration.TerminationRules)
                {
                    if (rule.GracePeriod is null)
                        continue;

                    // 1) Error Rate
                    if (rule.MaxErrorRate is not null && respSummaries is not null && respSummaries.Count > 0)
                    {
                        int total = 0, errors = 0;
                        foreach (var s in respSummaries)
                        {
                            int count = s.Count;
                            total += count;
                            if (Enum.TryParse<HttpStatusCode>(s.HttpStatusCode, true, out var code) &&
                                rule.ErrorStatusCodes.Contains(code))
                            {
                                errors += count;
                            }
                        }

                        var key = (httpIteration.Id, rule, TerminationMetricKind.ErrorRate);
                        var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));

                        if (await st.UpdateAndCheckRateAsync(total, errors, rule.MaxErrorRate.Value))
                        {
                            _terminatedIterations.TryAdd(httpIteration.Id, true);
                            return true;
                        }
                    }

                    // 2) Latency thresholds (P90/P50/P10/Average) — use fetched values; skip if no snapshot
                    if (duration is not null)
                    {
                        // P90
                        if (rule.P90Greater is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.P90);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.P90ResponseTime, rule.P90Greater.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);
                                return true;
                            }
                        }

                        // P50
                        if (rule.P50Greater is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.P50);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.P50ResponseTime, rule.P50Greater.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);
                                return true;
                            }
                        }

                        // P10
                        if (rule.P10Greater is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.P10);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.P10ResponseTime, rule.P10Greater.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);
                                return true;
                            }
                        }

                        // Average
                        if (rule.AVGGreater is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.Average);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.AverageResponseTime, rule.AVGGreater.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId,
                    $"Termination check failed\n {ex}",
                    LPSLoggingLevel.Error, token);
                return false;
            }
        }
    }
}
