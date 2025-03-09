using Grpc.Net.Client;
using LPS.Domain;
using LPS.Infrastructure.Nodes;
using Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Core.Services
{
    internal class EntityRegisterer
    {
        IClusterConfiguration _clusterConfiguration;
        private readonly INodeMetadata _nodeMetaData;
        public EntityRegisterer(IClusterConfiguration clusterConfiguration,
            INodeMetadata nodeMetaData) 
        { 
            _clusterConfiguration = clusterConfiguration;
            _nodeMetaData = nodeMetaData;
        }

        public void RegisterEntities(Plan plan)
        {
            var channel = GrpcChannel.ForAddress($"http://{_clusterConfiguration.MasterNodeIP}:{_clusterConfiguration.GRPCPort}");
            var grpcClient = new EntityDiscoveryProtoService.EntityDiscoveryProtoServiceClient(channel);
            foreach (var round in plan.GetReadOnlyRounds())
            {
                foreach (var iteration in round.GetReadOnlyIterations())
                {
                    if (((HttpIteration)iteration).HttpRequest != null)
                    {
                        var entityName = $"plan/{plan.Name}/round/{round.Name}/Iteration/{iteration.Name}";

                        var request = new Nodes.EntityDiscoveryRecord
                        {
                            FullyQualifiedName = entityName,
                            RoundId = round.Id.ToString(),
                            IterationId = iteration.Id.ToString(),
                            RequestId = ((HttpIteration)iteration).HttpRequest.Id.ToString(),
                            Node = new Nodes.Node
                            {
                                Name = _nodeMetaData.NodeName,
                                NodeIP = _nodeMetaData.NodeIP,
                            }
                        };

                        grpcClient.AddEntityDiscoveryRecord(request);
                    }
                }
            }
        }
    }
}
