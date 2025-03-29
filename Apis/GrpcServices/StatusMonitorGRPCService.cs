using LPS.Infrastructure.Nodes;
using Grpc.Core;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain;
using LPS.Protos.Shared;
using LPS.Infrastructure.Common.GRPCExtensions;
namespace Apis.Services
{
    public class StatusMonitorGRPCService : StatusMonitorService.StatusMonitorServiceBase
    {
        private readonly IEntityDiscoveryService _discoveryService;
        private readonly ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _statusMonitor;

        public StatusMonitorGRPCService(
            IEntityDiscoveryService discoveryService,
            ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> statusMonitor)
        {
            _discoveryService = discoveryService;
            _statusMonitor = statusMonitor;
        }

        public override async Task<StatusQueryResponse> QueryIterationStatuses(StatusQueryRequest request, ServerCallContext context)
        {
            // Discover entity by FQDN
            var record = _discoveryService
                .Discover(r => r.FullyQualifiedName == request.FullyQualifiedName)
                ?.FirstOrDefault();

            if (record == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"No entity found for FQDN: {request.FullyQualifiedName}"));
            }

            // Get statuses from status monitor
            var internalStatuses = (await _statusMonitor
                .Query(iteration => iteration.Id == record.IterationId))
                .Single()                
                .Value;

            // Map to gRPC-compatible enum
            var grpcStatuses = internalStatuses
                .Select(status=> status.ToGrpc())
                .ToList();

            var response = new StatusQueryResponse();
            response.Statuses.AddRange(grpcStatuses);

            return response;
        }

    }

}