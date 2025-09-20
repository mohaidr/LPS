using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.Nodes;
using System.Threading;

namespace LPS.Infrastructure.FailureEvaluator
{
    public class IterationFailureEvaluator : IIterationFailureEvaluator
    {
        private readonly ICommandStatusMonitor<HttpIteration> _commandStatusMonitor;
        private readonly INodeMetadata _nodeMetadata;
        private readonly IClusterConfiguration _clusterConfig;
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly IEntityDiscoveryService _discoveryService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

        public IterationFailureEvaluator(
            ICommandStatusMonitor<HttpIteration> commandStatusMonitor,
            INodeMetadata nodeMetadata,
            IClusterConfiguration clusterConfig,
            ICustomGrpcClientFactory grpcClientFactory,
            IEntityDiscoveryService discoveryService,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _commandStatusMonitor = commandStatusMonitor;
            _nodeMetadata = nodeMetadata;
            _clusterConfig = clusterConfig;
            _grpcClientFactory = grpcClientFactory;
            _discoveryService = discoveryService;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        /// <summary>
        /// Returns true if failure criteria are exceeded based on:
        /// - Error rate (if MaxErrorRate > 0 and ErrorStatusCodes provided), OR
        /// - Any duration threshold (P90/P50/P10/Average) if provided.
        /// If both kinds are configured, exceeding either causes failure.
        /// Logs the reason when failure is determined.
        /// </summary>
        public async Task<bool> IsErrorRateExceededAsync(HttpIteration iteration, CancellationToken token = default)
        {
            try
            {
                // Do not evaluate while commands are still running/scheduled (preserve original behavior)
                if ((await _commandStatusMonitor.QueryAsync(iteration))
                    .Any(status => status == CommandExecutionStatus.Ongoing || status == CommandExecutionStatus.Scheduled))
                {
                    return false;
                }

                var fc = iteration.FailureCriteria; // struct with nullable internals

                // Determine which criteria are active
                bool hasRateCriteria =
                    (fc.MaxErrorRate is > 0) &&
                    (fc.ErrorStatusCodes != null && fc.ErrorStatusCodes.Count > 0);

                bool hasDurationCriteria =
                    (fc.MaxP90 is > 0) ||
                    (fc.MaxP50 is > 0) ||
                    (fc.MaxP10 is > 0) ||
                    (fc.MaxAvg is > 0);

                // If nothing configured, nothing to evaluate
                if (!hasRateCriteria && !hasDurationCriteria)
                    return false;

                // Discover iteration FQDN for metrics lookups
                var fqdn = _discoveryService
                    .Discover(itr => itr.IterationId == iteration.Id)
                    .FirstOrDefault()?.FullyQualifiedName;

                if (fqdn == null)
                    throw new ArgumentException(nameof(fqdn));

                var grpcClient = _grpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfig.MasterNodeIP);

                // --- Error Rate evaluation (if configured)
                if (hasRateCriteria)
                {
                    var respCodes = await grpcClient.GetResponseCodesAsync(fqdn, token);
                    var respSummaries = respCodes?.Responses?.SingleOrDefault()?.Summaries;

                    if (respSummaries != null && respSummaries.Count > 0)
                    {
                        int total = 0, errors = 0;

                        foreach (var s in respSummaries)
                        {
                            int count = s.Count;
                            total += count;

                            if (Enum.TryParse<HttpStatusCode>(s.HttpStatusCode, true, out var code) &&
                                fc.ErrorStatusCodes!.Contains(code))
                            {
                                errors += count;
                            }
                        }

                        if (total > 0)
                        {
                            var actualErrorRate = (double)errors / total;
                            if (actualErrorRate > fc.MaxErrorRate!.Value)
                            {
                                await _logger.LogAsync(
                                    _runtimeOperationIdProvider.OperationId,
                                    $"Iteration '{iteration.Name}' determined as FAILED: Error rate exceeded. Actual={actualErrorRate:P2}, Threshold={fc.MaxErrorRate:P2}, " +
                                    $"TotalResponses={total}, ErrorResponses={errors}, TrackedCodes=[{string.Join(",", fc.ErrorStatusCodes!)}].",
                                    LPSLoggingLevel.Information,
                                    token);
                                return true;
                            }
                        }
                    }
                }

                // --- Duration thresholds evaluation (if configured)
                if (hasDurationCriteria)
                {
                    var durations = await grpcClient.GetDurationAsync(fqdn, token);
                    var duration = durations?.Responses?.SingleOrDefault();

                    if (duration is not null)
                    {
                        if (fc.MaxP90 is > 0 && duration.P90ResponseTime > fc.MaxP90.Value)
                        {
                            await _logger.LogAsync(
                                _runtimeOperationIdProvider.OperationId,
                                $"Iteration '{iteration.Name}' determined as FAILED: P90 response time exceeded. Actual={duration.P90ResponseTime} ms, Threshold={fc.MaxP90.Value} ms.",
                                LPSLoggingLevel.Information,
                                token);
                            return true;
                        }

                        if (fc.MaxP50 is > 0 && duration.P50ResponseTime > fc.MaxP50.Value)
                        {
                            await _logger.LogAsync(
                                _runtimeOperationIdProvider.OperationId,
                                $"Iteration '{iteration.Name}' determined as FAILED: P50 response time exceeded. Actual={duration.P50ResponseTime} ms, Threshold={fc.MaxP50.Value} ms.",
                                LPSLoggingLevel.Information,
                                token);
                            return true;
                        }

                        if (fc.MaxP10 is > 0 && duration.P10ResponseTime > fc.MaxP10.Value)
                        {
                            await _logger.LogAsync(
                                _runtimeOperationIdProvider.OperationId,
                                $"Iteration '{iteration.Name}' determined as FAILED: P10 response time exceeded. Actual={duration.P10ResponseTime} ms, Threshold={fc.MaxP10.Value} ms.",
                                LPSLoggingLevel.Information,
                                token);
                            return true;
                        }

                        if (fc.MaxAvg is > 0 && duration.AverageResponseTime > fc.MaxAvg.Value)
                        {
                            await _logger.LogAsync(
                                _runtimeOperationIdProvider.OperationId,
                                $"Iteration '{iteration.Name}' determined as FAILED: Average response time exceeded. Actual={duration.AverageResponseTime} ms, Threshold={fc.MaxAvg.Value} ms.",
                                LPSLoggingLevel.Information,
                                token);
                            return true;
                        }
                    }
                }

                return false;
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
    }
}
