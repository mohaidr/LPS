using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.Nodes;
using System.Threading;
using LPS.Infrastructure.Monitoring;  // NEW: For MetricParser
using System.Collections.Concurrent;

namespace LPS.Infrastructure.FailureEvaluator
{
    public class IterationFailureEvaluator : IIterationFailureEvaluator
    {
        // Cache parsed metric expressions to avoid repeated regex parsing
        private readonly ConcurrentDictionary<string, (string MetricName, ComparisonOperator Op, double Threshold, double? ThresholdMax)> _parsedMetricCache = new();

        private readonly ICommandStatusMonitor<HttpIteration> _commandStatusMonitor;
        private readonly INodeMetadata _nodeMetadata;
        private readonly IClusterConfiguration _clusterConfig;
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly IEntityDiscoveryService _discoveryService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private readonly IMetricFetcher _metricFetcher;  // NEW: Injected dependency

        public IterationFailureEvaluator(
            ICommandStatusMonitor<HttpIteration> commandStatusMonitor,
            INodeMetadata nodeMetadata,
            IClusterConfiguration clusterConfig,
            ICustomGrpcClientFactory grpcClientFactory,
            IEntityDiscoveryService discoveryService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IMetricFetcher metricFetcher)  // NEW: Inject MetricFetcher
        {
            _commandStatusMonitor = commandStatusMonitor;
            _nodeMetadata = nodeMetadata;
            _clusterConfig = clusterConfig;
            _grpcClientFactory = grpcClientFactory;
            _discoveryService = discoveryService;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _metricFetcher = metricFetcher ?? throw new ArgumentNullException(nameof(metricFetcher));  // NEW
        }

        /// <summary>
        /// Returns true if failure criteria are exceeded based on configured failure rules.
        /// If no failure rules configured, applies default failure rules (ErrorRate > 5% OR Any 5xx).
        /// Note: Termination rules are evaluated separately and take precedence over failure rules.
        /// Logs the reason when failure is determined.
        /// </summary>
        public async Task<bool> EvaluateFailureAsync(HttpIteration iteration, CancellationToken token = default)
        {
            try
            {
                // Do not evaluate while commands are still running/scheduled
                // NOTE: We DO evaluate after completion even if status is "Ongoing"
                var commandStatuses = await _commandStatusMonitor.QueryAsync(iteration);
                if (commandStatuses.Any(status => status == CommandExecutionStatus.Scheduled))
                {
                    // Don't evaluate if commands haven't started yet
                    return false;
                }

                // Check if using inline operator syntax
                if (iteration.FailureRules != null && iteration.FailureRules.Count > 0)
                {
                    return await EvaluateInlineFailureRulesAsync(iteration, token);
                }

                // No rules configured at all - apply default failure rules
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"No failure criteria configured for iteration '{iteration.Name}'. Applying default failure rules: ErrorRate > 5% OR Any 5xx.",
                    LPSLoggingLevel.Information,
                    token);
                
                return await EvaluateDefaultFailureRulesAsync(iteration, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"IterationFailureEvaluator failed to check thresholds\n {ex}",
                    LPSLoggingLevel.Error,
                    token);
                return false;
            }
        }

        /// <summary>
        /// Evaluates failure rules using inline operator syntax.
        /// </summary>
        private async Task<bool> EvaluateInlineFailureRulesAsync(HttpIteration iteration, CancellationToken token)
        {
            try
            {
                var fqdn = _discoveryService
                    .Discover(itr => itr.IterationId == iteration.Id)
                    .FirstOrDefault()?.FullyQualifiedName;

                if (fqdn == null)
                    return false;

                foreach (var rule in iteration.FailureRules)
                {
                    try
                    {
                        // Parse the metric expression (cached to avoid repeated regex parsing)
                        var (metricName, op, threshold, thresholdMax) = GetOrParseCached(rule.Metric);

                        // Use injected MetricFetcher - pass ErrorStatusCodes for ErrorRate metrics
                        double currentValue = await _metricFetcher.GetMetricValueAsync(fqdn, metricName, rule.ErrorStatusCodes, token);

                        // Evaluate the condition
                        bool conditionMet = MetricParser.EvaluateCondition(currentValue, op, threshold, thresholdMax);
                        
                        if (conditionMet)
                        {
                            var statusCodesInfo = !string.IsNullOrEmpty(rule.ErrorStatusCodes) 
                                ? $" (ErrorStatusCodes: {rule.ErrorStatusCodes})" 
                                : "";
                            await _logger.LogAsync(
                                _runtimeOperationIdProvider.OperationId,
                                $"Iteration '{iteration.Name}' determined as FAILED: Rule '{rule.Metric}'{statusCodesInfo} triggered. Current value: {currentValue}",
                                LPSLoggingLevel.Warning,
                                token);
                            return true;
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        await _logger.LogAsync(
                            _runtimeOperationIdProvider.OperationId,
                            $"Invalid failure rule '{rule.Metric}': {ex.Message}",
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
                    $"Failed to evaluate inline failure rules: {ex.Message}",
                    LPSLoggingLevel.Error,
                    token);
                return false;
            }
        }

        /// <summary>
        /// Evaluates default failure rules when no criteria are configured.
        /// Default rules: ErrorRate > 5% OR StatusCode >= 500
        /// </summary>
        private async Task<bool> EvaluateDefaultFailureRulesAsync(HttpIteration iteration, CancellationToken token)
        {
            try
            {
                var fqdn = _discoveryService
                    .Discover(itr => itr.IterationId == iteration.Id)
                    .FirstOrDefault()?.FullyQualifiedName;

                if (fqdn == null)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        $"Default failure evaluation: FQDN not found for iteration '{iteration.Name}'",
                        LPSLoggingLevel.Warning,
                        token);
                    return false;
                }

                var grpcClient = _grpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfig.MasterNodeIP);
                var respCodes = await grpcClient.GetResponseCodesAsync(fqdn, token);
                var respSummaries = respCodes?.Responses?.SingleOrDefault()?.Summaries;

                if (respSummaries == null || respSummaries.Count == 0)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        $"Default failure evaluation: No response summaries available yet for iteration '{iteration.Name}' - CANNOT DETERMINE FAILURE YET",
                        LPSLoggingLevel.Information,
                        token);
                    // Return false but DON'T let IterationStatusMonitor cache Success yet
                    // This is critical - we need response data before determining final status
                    return false;
                }

                int total = 0, serverErrors = 0, allErrors = 0;

                foreach (var s in respSummaries)
                {
                    int count = s.Count;
                    total += count;

                    int codeInt;
                    
                    // Try parsing as integer first (e.g., "401")
                    if (int.TryParse(s.HttpStatusCode, out codeInt))
                    {
                        // Successfully parsed as integer
                    }
                    // Try parsing as HttpStatusCode enum name (e.g., "Unauthorized" -> 401)
                    else if (Enum.TryParse<HttpStatusCode>(s.HttpStatusCode, true, out var statusCode))
                    {
                        codeInt = (int)statusCode;
                    }
                    else
                    {
                        // Unknown status code format - skip this entry
                        continue;
                    }

                    // Server errors (>= 500)
                    if (codeInt >= 500)
                    {
                        serverErrors += count;
                    }

                    // All errors (>= 400)
                    if (codeInt >= 400)
                    {
                        allErrors += count;
                    }
                }

                if (total == 0)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        $"Default failure evaluation: No requests recorded for iteration '{iteration.Name}'",
                        LPSLoggingLevel.Information,
                        token);
                    return false;
                }

                // Default Rule 1: ErrorRate > 5%
                var errorRate = (double)allErrors / total;
                
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Default failure evaluation for '{iteration.Name}': ErrorRate={errorRate:P2} ({allErrors}/{total}), ServerErrors={serverErrors}",
                    LPSLoggingLevel.Information,
                    token);
                
                if (errorRate > 0.05)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        $"Iteration '{iteration.Name}' determined as FAILED (DEFAULT RULE): Error rate exceeded 5%. Actual={errorRate:P2}, " +
                        $"TotalResponses={total}, ErrorResponses={allErrors} (4xx+5xx).",
                        LPSLoggingLevel.Warning,
                        token);
                    return true;
                }

                // Default Rule 2: Any server error (>= 500)
                if (serverErrors > 0)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        $"Iteration '{iteration.Name}' determined as FAILED (DEFAULT RULE): Server errors detected. " +
                        $"TotalResponses={total}, ServerErrors={serverErrors} (5xx).",
                        LPSLoggingLevel.Warning,
                        token);
                    return true;
                }

                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Default failure evaluation: Iteration '{iteration.Name}' passed all checks (ErrorRate={errorRate:P2}, ServerErrors={serverErrors})",
                    LPSLoggingLevel.Information,
                    token);

                return false;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"Failed to evaluate default failure rules: {ex.Message}",
                    LPSLoggingLevel.Error,
                    token);
                return false;
            }
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
