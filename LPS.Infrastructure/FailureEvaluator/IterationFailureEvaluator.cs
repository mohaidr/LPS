using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Infrastructure.Monitoring.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using LPS.Infrastructure.Logger;
using YamlDotNet.Core.Tokens;
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
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        public IterationFailureEvaluator(
            ICommandStatusMonitor<HttpIteration> commandStatusMonitor,
            INodeMetadata nodeMetadata,
            IClusterConfiguration clusterConfig,
            ICustomGrpcClientFactory grpcClientFactory,
            IEntityDiscoveryService discoveryService, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _commandStatusMonitor = commandStatusMonitor;
            _nodeMetadata = nodeMetadata;
            _clusterConfig = clusterConfig;
            _grpcClientFactory = grpcClientFactory;
            _discoveryService = discoveryService;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;

        }

        public async Task<bool> IsErrorRateExceededAsync(HttpIteration iteration, CancellationToken token = default)
        {
            try
            {
                if (!iteration.MaxErrorRate.HasValue)
                {
                    return false;
                }

                if ((await _commandStatusMonitor.QueryAsync(iteration)).Any(status => status == CommandExecutionStatus.Ongoing || status == CommandExecutionStatus.Scheduled))
                    return false;

                if (iteration.MaxErrorRate <= 0 || iteration.ErrorStatusCodes == null)
                    return false;

                var fqdn = _discoveryService.Discover(itr => itr.IterationId == iteration.Id).FirstOrDefault()?.FullyQualifiedName;
                if (fqdn == null)
                    throw new ArgumentException(nameof(fqdn));

                var grpcClient = _grpcClientFactory.GetClient<GrpcMetricsQueryServiceClient>(_clusterConfig.MasterNodeIP);
                var response = await grpcClient.GetResponseCodesAsync(fqdn);

                var summaries = response?.Responses?.SingleOrDefault()?.Summaries;
                if (summaries == null || summaries.Count == 0)
                    return false;


                int total = 0;
                int errors = 0;

                foreach (var summary in summaries)
                {
                    int count = summary.Count;
                    Enum.TryParse(summary.HttpStatusCode, true, out HttpStatusCode code);

                    total += count;
                    if (iteration.ErrorStatusCodes.Contains(code))
                        errors += count;
                }

                if (total == 0)
                    return false;

                var actualErrorRate = (double)errors / total;
                return actualErrorRate > (iteration.MaxErrorRate.Value);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"IterationFailureEvaluator failed to check error rate\n {ex}", LPSLoggingLevel.Error, token);
                return false;
            }
        }
    }
}
