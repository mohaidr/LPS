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
    public class HttpIterationTerminationCheckerService : ITerminationCheckerService
    {
        // Track grace state per (iteration, metric expression)
        private readonly ConcurrentDictionary<(Guid, string), GracePeriodState> _stateV2 = new();

        private readonly ConcurrentDictionary<Guid, bool> _terminatedIterations = new();

        // Cache parsed metric expressions to avoid repeated regex parsing
        private readonly ConcurrentDictionary<string, (string MetricName, ComparisonOperator Op, double Threshold, double? ThresholdMax)> _parsedMetricCache = new();

        private readonly IEntityDiscoveryService _discoveryService;
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly IClusterConfiguration _clusterConfig;
        private readonly INodeMetadata _nodeMetadata;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IMetricFetcher _metricFetcher;

        private readonly GrpcIterationTerminationServiceClient _grpcTermClient;

        public HttpIterationTerminationCheckerService(
            IEntityDiscoveryService discoveryService,
            ICustomGrpcClientFactory grpcClientFactory,
            IClusterConfiguration clusterConfig,
            INodeMetadata nodeMetadata,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricFetcher metricFetcher)
        {
            _discoveryService = discoveryService;
            _grpcClientFactory = grpcClientFactory;
            _clusterConfig = clusterConfig;
            _nodeMetadata = nodeMetadata;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _metricFetcher = metricFetcher ?? throw new ArgumentNullException(nameof(metricFetcher));

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

                var fqdn = _discoveryService
                    .Discover(r => r.IterationId == httpIteration.Id)
                    .FirstOrDefault()?.FullyQualifiedName;

                if (_nodeMetadata.NodeType == Nodes.NodeType.Worker)
                {
                    if (fqdn == null)
                        throw new ArgumentException(nameof(fqdn));

                    return await _grpcTermClient.IsTerminatedAsync(fqdn);
                }

                // Check if using inline operator syntax (V2)
                if (httpIteration.TerminationRules != null && httpIteration.TerminationRules.Count > 0)
                {
                    return await EvaluateInlineTerminationRulesAsync(httpIteration, fqdn, token);
                }

                // No rules configured
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

        /// <summary>
        /// NEW: Evaluates termination rules using inline operator syntax (V2).
        /// </summary>
        private async Task<bool> EvaluateInlineTerminationRulesAsync(HttpIteration iteration, string fqdn, CancellationToken token)
        {
            try
            {
                foreach (var rule in iteration.TerminationRules)
                {
                    try
                    {
                        // Parse the metric expression (cached to avoid repeated regex parsing)
                        var (metricName, op, threshold, thresholdMax) = GetOrParseCached(rule.Metric);

                        // Use injected MetricFetcher - pass ErrorStatusCodes for ErrorRate metrics
                        double currentValue = await _metricFetcher.GetMetricValueAsync(fqdn, metricName, rule.ErrorStatusCodes, token);

                        // Check if condition is met (threshold violated)
                        bool conditionMet = MetricParser.EvaluateCondition(currentValue, op, threshold, thresholdMax);

                        // Use grace period tracking
                        var key = (iteration.Id, rule.Metric);
                        var graceState = _stateV2.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod));

                        if (await graceState.UpdateAndCheckValueAsync(conditionMet ? 1 : 0, 0.5))
                        {
                            _terminatedIterations.TryAdd(iteration.Id, true);

                            var statusCodesInfo = !string.IsNullOrEmpty(rule.ErrorStatusCodes) 
                                ? $" (ErrorStatusCodes: {rule.ErrorStatusCodes})" 
                                : "";
                            await _logger.LogAsync(
                                _runtimeOperationIdProvider.OperationId,
                                $"The iteration '{iteration.Name}' will be terminated because the rule '{rule.Metric}'{statusCodesInfo} was violated " +
                                $"for the entire grace period of {rule.GracePeriod.TotalSeconds}s. Current value: {currentValue}",
                                LPSLoggingLevel.Warning,
                                token);
                            return true;
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"Invalid termination rule '{rule.Metric}': {ex.Message}",
                            LPSLoggingLevel.Error,
                            token);
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Failed to evaluate inline termination rules: {ex.Message}",
                    LPSLoggingLevel.Error,
                    token);
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

        /// <summary>
        /// Gets a cached parsed metric expression or parses and caches it.
        /// </summary>
        private (string MetricName, ComparisonOperator Op, double Threshold, double? ThresholdMax) GetOrParseCached(string metricExpression)
        {
            return _parsedMetricCache.GetOrAdd(metricExpression, expr => MetricParser.Parse(expr));
        }
    }
}
