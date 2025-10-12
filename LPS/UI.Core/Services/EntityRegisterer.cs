using Grpc.Net.Client;
using LPS.Domain;
using LPS.Infrastructure.Entity;
using LPS.Infrastructure.GRPCClients;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;

namespace LPS.UI.Core.Services
{
    internal class EntityRegisterer
    {
        IClusterConfiguration _clusterConfiguration;
        IEntityDiscoveryService _entityDiscoveryService;
        private readonly INodeMetadata _nodeMetaData;
        INodeRegistry _nodeRegistry; IEntityRepositoryService _entityRepositoryService;
        ICustomGrpcClientFactory _customGrpcClientFactory;
        public EntityRegisterer(IClusterConfiguration clusterConfiguration,
            INodeMetadata nodeMetaData, 
            IEntityDiscoveryService entityDiscoveryService, 
            INodeRegistry nodeRegistry, IEntityRepositoryService entityRepositoryService,
            ICustomGrpcClientFactory customGrpcClientFactory) 
        { 
            _clusterConfiguration = clusterConfiguration;
            _nodeMetaData = nodeMetaData;
            _nodeRegistry = nodeRegistry;
            _entityDiscoveryService = entityDiscoveryService;
            _customGrpcClientFactory = customGrpcClientFactory;
            _entityRepositoryService = entityRepositoryService;
        }

        public async ValueTask RegisterEntitiesAsync(Plan plan)
        {
            // TODO: This _entityRepositoryService registration logic na possibly the whole RegisterEntities logic should eventually called during the setup process not the execution.
            // For now, it remains here to maintain development momentum, since there’s no database logic or EF implementation yet.
            // The service is used to register and retrieve entities during execution, particularly for resolving local entities 
            // For example, when operating in distributed mode.
            _entityRepositoryService.Add(plan);
            var grpcClient = _customGrpcClientFactory.GetClient<GrpcEntityDiscoveryClient>(_clusterConfiguration.MasterNodeIP);
            foreach (var round in plan.GetReadOnlyRounds())
            {
                _entityRepositoryService.Add(round);
                foreach (var iteration in round.GetReadOnlyIterations())
                {
                    if (iteration is HttpIteration)
                    {
                        _entityRepositoryService.Add((HttpIteration)iteration);

                        if (((HttpIteration)iteration).HttpRequest != null)
                        {
                            var fqdn = $"plan/{plan.Name}/round/{round.Name}/Iteration/{iteration.Name}";
                            _entityDiscoveryService.AddEntityDiscoveryRecord(fqdn, round.Id, iteration.Id, ((HttpIteration)iteration).HttpRequest.Id, _nodeRegistry.GetLocalNode()); // register locally
                            if (_nodeMetaData.NodeType != Infrastructure.Nodes.NodeType.Master)
                            {
                                var request = new Protos.Shared.EntityDiscoveryRecord
                                {
                                    FullyQualifiedName = fqdn,
                                    RoundId = round.Id.ToString(),
                                    IterationId = iteration.Id.ToString(),
                                    RequestId = ((HttpIteration)iteration).HttpRequest.Id.ToString(),
                                    Node = new Protos.Shared.Node
                                    {
                                        Name = _nodeMetaData.NodeName,
                                        NodeIP = _nodeMetaData.NodeIP,
                                    }
                                };

                               await grpcClient.AddEntityDiscoveryRecordAsync(request);// register on the master
                            }
                        }
                    }
                    else { 
                        throw new NotImplementedException();
                    }
                }
            }
        }
    }
}
