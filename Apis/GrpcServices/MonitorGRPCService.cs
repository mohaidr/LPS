using LPS.Infrastructure.Nodes;
using Grpc.Core;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Protos.Shared;
using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Infrastructure.Logger;
using ILogger = LPS.Domain.Common.Interfaces.ILogger;
namespace Apis.Services
{
    public class MonitorGRPCService : MonitorService.MonitorServiceBase
    {
        private readonly IEntityDiscoveryService _discoveryService;
        private readonly ICommandStatusMonitor<HttpIteration> _statusMonitor;
        private readonly IMetricsDataMonitor _metricsMonitor;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        INodeMetadata _nodeMetadata;
        public MonitorGRPCService(
            IEntityDiscoveryService discoveryService,
            ICommandStatusMonitor<HttpIteration> statusMonitor,
             IMetricsDataMonitor metricsMonitor, 
             ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider, INodeMetadata nodeMetadata)
        {
            _discoveryService = discoveryService;
            _statusMonitor = statusMonitor;
            _metricsMonitor = metricsMonitor;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _nodeMetadata = nodeMetadata;
        }

        public override async Task<StatusQueryResponse> QueryIterationStatuses(StatusQueryRequest request, ServerCallContext context)
        {
            // Discover entity by FQDN
            var record = _discoveryService
                .Discover(r => r.FullyQualifiedName == request.FullyQualifiedName && 
                            r.Node.Metadata.NodeType == _nodeMetadata.NodeType)?.SingleOrDefault();

            if (record == null)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"No entity found for FQDN: {request.FullyQualifiedName}", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.NotFound, $"No entity found for FQDN: {request.FullyQualifiedName}"));
            }

            // Get statuses from status monitor
            var internalStatuses = (await _statusMonitor
                .QueryAsync(iteration => iteration.Id == record.IterationId))
                .SingleOrDefault()
                .Value;

            // Map to gRPC-compatible enum
            var grpcStatuses = internalStatuses?
                .Select(status=> status.ToGrpc())
                .ToList();

            var response = new StatusQueryResponse();
            response.Statuses.AddRange(grpcStatuses?? new List<LPS.Protos.Shared.ExecutionStatus>());
            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Status Request Completed Successfully: {request.FullyQualifiedName}", LPSLoggingLevel.Verbose);

            return response;
        }
        public override Task<MonitorResponse> Monitor(MonitorRequest request, ServerCallContext context)
        {
            var record = _discoveryService
                .Discover(r => r.FullyQualifiedName == request.FullyQualifiedName &&
                            r.Node.Metadata.NodeType == _nodeMetadata.NodeType)?.SingleOrDefault();

            if (record == null)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"Can't Monitor {request.FullyQualifiedName} - No entity found for FQDN: {request.FullyQualifiedName}", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.NotFound, $"Can't Monitor {request.FullyQualifiedName} - No entity found for FQDN: {request.FullyQualifiedName}"));
            }


            _metricsMonitor.Monitor(iteration=> iteration.Id == record.IterationId);
            _logger.Log(_runtimeOperationIdProvider.OperationId, $"gRPC monitor request completed successfully: {request.FullyQualifiedName}", LPSLoggingLevel.Verbose);

            return Task.FromResult(new MonitorResponse
            {
                Success = true,
                Message = $"Monitoring started for: {record.FullyQualifiedName}"
            });
        }
    }
}