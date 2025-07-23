using LPS.Domain;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;

namespace LPS.Infrastructure.Monitoring.TerminationServices
{
    public class HttpIterationTerminationCheckerService : ITerminationCheckerService
    {
        private readonly ConcurrentDictionary<(Guid, TerminationRule), GracePeriodState> _state = new();
        private readonly ConcurrentDictionary<Guid, bool> _terminatedIterations = new();
        private readonly IEntityDiscoveryService _discoveryService;
        private readonly ICustomGrpcClientFactory _grpcClientFactory;
        private readonly IClusterConfiguration _clusterConfig;
        private readonly INodeMetadata _nodeMetadata;
        GrpcIterationTerminationServiceClient _grpcClient;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public HttpIterationTerminationCheckerService(
            IEntityDiscoveryService discoveryService,
            ICustomGrpcClientFactory grpcClientFactory,
            IClusterConfiguration clusterConfig,
            INodeMetadata nodeMetadata, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _discoveryService = discoveryService;
            _grpcClientFactory = grpcClientFactory;
            _clusterConfig = clusterConfig;
            _nodeMetadata = nodeMetadata;
            _grpcClient = _grpcClientFactory.GetClient<GrpcIterationTerminationServiceClient>(_clusterConfig.MasterNodeIP);
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public async Task<bool> IsTerminationRequiredAsync(Iteration iteration, CancellationToken token = default)
        {
            try
            {
                if (iteration is not HttpIteration httpIteration)
                    throw new ArgumentException("Expected HttpIteration", nameof(iteration));

                if (_terminatedIterations.ContainsKey(httpIteration.Id))
                {
                    return true;
                }

                if (httpIteration.TerminationRules == null || !httpIteration.TerminationRules.Any())
                    return false;

                var fqdn = _discoveryService.Discover(record => record.IterationId == httpIteration.Id).FirstOrDefault()?.FullyQualifiedName;

                // 🟨 If current node is worker — delegate to master via gRPC
                if (_nodeMetadata.NodeType == Nodes.NodeType.Worker)
                {
                    if(fqdn == null)
                         throw new ArgumentException(nameof(fqdn));

                    return await _grpcClient.IsTerminatedAsync(fqdn);
                }

                // 🟩 Local termination logic for master node
                var metricsClient = _grpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfig.MasterNodeIP);
                var response = await metricsClient.GetResponseCodesAsync(fqdn);

                var summaries = response?.Responses?.SingleOrDefault()?.Summaries;
                if (summaries == null || summaries.Count == 0)
                    return false;

                foreach (var rule in httpIteration.TerminationRules)
                {
                    if (rule.MaxErrorRate is null || rule.GracePeriod is null)
                        continue;

                    var key = (httpIteration.Id, rule);
                    var state = _state.GetOrAdd(key, _ => new GracePeriodState(rule.GracePeriod.Value));

                    int total = 0;
                    int errors = 0;

                    foreach (var summary in summaries)
                    {
                        int count = summary.Count;
                        Enum.TryParse(summary.HttpStatusCode, true, out HttpStatusCode code);

                        total += count;
                        if (rule.ErrorStatusCodes.Contains(code))
                            errors += count;
                    }

                    if (await state.UpdateAndCheckAsync(total, errors, rule.MaxErrorRate.Value))
                    {
                        _terminatedIterations.TryAdd(httpIteration.Id, true);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)   
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Termination failed\n {ex}", LPSLoggingLevel.Error, token);
                return false;
            }
        }
    }
}
