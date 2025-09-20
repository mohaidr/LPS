// HttpIterationTerminationCheckerService.cs (fully updated)
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

                // Master: fetch metrics
                var metricsClient = _grpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfig.MasterNodeIP);

                var respCodes = await metricsClient.GetResponseCodesAsync(fqdn, token);
                var respSummaries = respCodes?.Responses?.SingleOrDefault()?.Summaries;

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

                            await LogTerminationAsync(httpIteration,
                                "Error Rate",
                                $"{(double)errors / total:P}",
                                $"{rule.MaxErrorRate:P}",
                                rule.GracePeriod.Value, token);

                            return true;
                        }
                    }

                    if (duration is not null)
                    {
                        // P90
                        if (rule.MaxP90 is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.P90);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.P90ResponseTime, rule.MaxP90.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);

                                await LogTerminationAsync(httpIteration,
                                    "P90 response time",
                                    $"{duration.P90ResponseTime} ms",
                                    $"{rule.MaxP90.Value} ms",
                                    rule.GracePeriod.Value, token);

                                return true;
                            }
                        }

                        // P50
                        if (rule.MaxP50 is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.P50);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.P50ResponseTime, rule.MaxP50.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);

                                await LogTerminationAsync(httpIteration,
                                    "P50 response time",
                                    $"{duration.P50ResponseTime} ms",
                                    $"{rule.MaxP50.Value} ms",
                                    rule.GracePeriod.Value, token);

                                return true;
                            }
                        }

                        // P10
                        if (rule.MaxP10 is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.P10);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.P10ResponseTime, rule.MaxP10.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);

                                await LogTerminationAsync(httpIteration,
                                    "P10 response time",
                                    $"{duration.P10ResponseTime} ms",
                                    $"{rule.MaxP10.Value} ms",
                                    rule.GracePeriod.Value, token);

                                return true;
                            }
                        }

                        // Average
                        if (rule.MaxAvg is not null)
                        {
                            var key = (httpIteration.Id, rule, TerminationMetricKind.Average);
                            var st = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));
                            if (await st.UpdateAndCheckValueAsync(duration.AverageResponseTime, rule.MaxAvg.Value))
                            {
                                _terminatedIterations.TryAdd(httpIteration.Id, true);

                                await LogTerminationAsync(httpIteration,
                                    "Average response time",
                                    $"{duration.AverageResponseTime} ms",
                                    $"{rule.MaxAvg.Value} ms",
                                    rule.GracePeriod.Value, token);

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

        private async Task LogTerminationAsync(HttpIteration iteration, string metricName, string actual, string threshold, TimeSpan gracePeriod, CancellationToken token)
        {
            await _logger.LogAsync(
                _runtimeOperationIdProvider.OperationId,
                $"The iteration {iteration.Name} will be terminated because the '{metricName}' exceeded the threshold {threshold} (actual: {actual}) after a grace period of {gracePeriod.TotalSeconds}s.",
                LPSLoggingLevel.Information,
                token);
        }
    }
}
