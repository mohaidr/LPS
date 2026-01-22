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
        CancellationTokenSource _cts;
        public MonitorGRPCService(
                IEntityDiscoveryService discoveryService,
                ICommandStatusMonitor<HttpIteration> statusMonitor,
                IMetricsDataMonitor metricsMonitor, 
                ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider,
                INodeMetadata nodeMetadata, CancellationTokenSource cts)
        {
            _discoveryService = discoveryService;
            _statusMonitor = statusMonitor;
            _metricsMonitor = metricsMonitor;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _nodeMetadata = nodeMetadata;
            _cts = cts;
        }

        public override async Task<StatusQueryResponse> QueryIterationStatuses(StatusQueryRequest request, ServerCallContext context)
        {
            // Discover entity by FQDN
            IEntityDiscoveryRecord? record = await GetRecordAsync(request.FullyQualifiedName);

            if (record == null)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"MonitorGRPCService.Monitor(): No entity found for FQDN: {request.FullyQualifiedName}", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.NotFound, $"MonitorGRPCService.Monitor(): No entity found for FQDN: {request.FullyQualifiedName}"));
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
        public override async Task<MonitorResponse> Monitor(MonitorRequest request, ServerCallContext context)
        {

            IEntityDiscoveryRecord? record = await GetRecordAsync(request.FullyQualifiedName);


            if (record == null)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, $"MonitorGRPCService.QueryIterationStatuses(): Can't Monitor {request.FullyQualifiedName} - No entity found for FQDN: {request.FullyQualifiedName}", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.NotFound, $"MonitorGRPCService.QueryIterationStatuses(): Can't Monitor {request.FullyQualifiedName} - No entity found for FQDN: {request.FullyQualifiedName}"));
            }

            // Metrics collectors are self-managing via IIterationStatusMonitor
            // They start on registration (TryRegisterAsync) and stop when iteration reaches terminal status
            _logger.Log(_runtimeOperationIdProvider.OperationId, $"gRPC monitor request completed successfully: {request.FullyQualifiedName}", LPSLoggingLevel.Verbose);

            return new MonitorResponse
            {
                Success = true,
                Message = $"Monitoring started for: {record.FullyQualifiedName}"
            };
        }
        private async Task<IEntityDiscoveryRecord?> GetRecordAsync(string fullyQualifiedName)
        {
            var delaySeconds = 1;
            var maxDelaySeconds = 32;
            IEntityDiscoveryRecord? record = null;

            while (record is null && delaySeconds <= maxDelaySeconds)
            {
                record = _discoveryService
                    .Discover(r => r.FullyQualifiedName == fullyQualifiedName &&
                                   r.Node.Metadata.NodeType == _nodeMetadata.NodeType)
                    ?.SingleOrDefault();

                if (record is not null)
                    break;

                await _logger.LogAsync(
                    _runtimeOperationIdProvider.OperationId,
                    $"GetRecordAsync(): No entity found for FQDN: {fullyQualifiedName}. Retrying in {delaySeconds} seconds...",
                    LPSLoggingLevel.Verbose);
                Console.WriteLine($"BackOff Logic: No entity found for FQDN: {fullyQualifiedName}. Retrying in {delaySeconds} seconds...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                // Exponential backoff: 1, 2,4, 8, 16, 32 (capped)
                delaySeconds = Math.Min(delaySeconds * 2, maxDelaySeconds);
            }
            return record;
        }


    }
}