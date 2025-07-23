using Grpc.Core;
using LPS.Domain;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Entity;
using LPS.Infrastructure.Monitoring.TerminationServices;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;

namespace Apis.GrpcServices
{

    public class IterationTerminationGrpcService : IterationTerminationService.IterationTerminationServiceBase
    {
        private readonly ITerminationCheckerService _terminationCheckerService;
        IEntityRepositoryService _entityRepoService;
        IEntityDiscoveryService _entityDiscoveryService;
        INodeMetadata _nodeMetadata;
        public IterationTerminationGrpcService(ITerminationCheckerService terminationCheckerService, IEntityRepositoryService entityRepoService, IEntityDiscoveryService entityDiscoveryService, INodeMetadata nodeMetadata)
        {
            _terminationCheckerService = terminationCheckerService;
            _entityRepoService = entityRepoService;
            _entityDiscoveryService = entityDiscoveryService;
            _nodeMetadata = nodeMetadata;
        }

        public override async Task<IsTerminatedResponse> IsTerminated(IsTerminatedByFQDNRequest request, ServerCallContext context)
        {
            var iterationId = _entityDiscoveryService.Discover(r => r.FullyQualifiedName == request.FullyQualifiedName && r.Node.Metadata.NodeType == _nodeMetadata.NodeType)?.SingleOrDefault()?.IterationId;

            if (iterationId == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"No iteration found for FQDN: {request.FullyQualifiedName}"));
            }

            var iteration = _entityRepoService.Get<HttpIteration>(iterationId.Value);
            bool result = await _terminationCheckerService.IsTerminationRequiredAsync(iteration, context.CancellationToken);
            return new IsTerminatedResponse { IsTerminated = result };
        }
    }
}
