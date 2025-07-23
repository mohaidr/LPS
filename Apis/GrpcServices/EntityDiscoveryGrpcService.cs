using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Threading.Tasks;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using Node = LPS.Protos.Shared.Node;
using LPS.Domain.Common.Interfaces;
using ILogger = LPS.Domain.Common.Interfaces.ILogger;

namespace LPS.GrpcServices
{
    public class EntityDiscoveryGrpcService : EntityDiscoveryProtoService.EntityDiscoveryProtoServiceBase
    {
        private readonly IEntityDiscoveryService _entityDiscoveryService;
        private readonly INodeRegistry _nodeRegistry;
        ILogger _logger;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        INodeMetadata _nodeMetaData;
        public EntityDiscoveryGrpcService(IEntityDiscoveryService entityDiscoveryService, 
            INodeRegistry nodeRegistry, ILogger logger , IRuntimeOperationIdProvider runtimeOperationIdProvider, INodeMetadata nodeMetaData)
        {
            _entityDiscoveryService = entityDiscoveryService;
            _nodeRegistry = nodeRegistry;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _nodeMetaData = nodeMetaData;
        }

        public override Task<Empty> AddEntityDiscoveryRecord(Protos.Shared.EntityDiscoveryRecord record, ServerCallContext context)
        {
            var node = _nodeRegistry.Query(n => n.Metadata.NodeName == record.Node.Name && n.Metadata.NodeIP == record.Node.NodeIP).FirstOrDefault();
            if (node == null) {
                _logger.Log(_runtimeOperationIdProvider.OperationId, "Node is not registered", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.NotFound, "Node is not registered"));
            }
            if (string.IsNullOrWhiteSpace(record.FullyQualifiedName) ||
                string.IsNullOrWhiteSpace(record.RoundId) ||
                string.IsNullOrWhiteSpace(record.IterationId) ||
                string.IsNullOrWhiteSpace(record.RequestId))
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, "FullyQualifiedName and entities Ids (RoundId, IterationId, RequestId) must be provided.", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.InvalidArgument, "FullyQualifiedName and entities Ids (RoundId, IterationId, RequestId) must be provided."));
            }

            _entityDiscoveryService.AddEntityDiscoveryRecord(
                record.FullyQualifiedName,
                Guid.Parse(record.RoundId),
                Guid.Parse(record.IterationId),
                Guid.Parse(record.RequestId),node);
            return Task.FromResult(new Empty());
        }

        public override Task<EntityDiscoveryRecordResponse> DiscoverEntity(EntityDiscoveryQuery query, ServerCallContext context)
        {
            if (string.IsNullOrWhiteSpace(query.FullyQualifiedName))
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, "FullyQualifiedName must be provided.", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.InvalidArgument, "FullyQualifiedName must be provided."));
            }
            var record = _entityDiscoveryService.Discover(r=> r.FullyQualifiedName == query.FullyQualifiedName && r.Node.Metadata.NodeType == _nodeMetaData.NodeType)?.SingleOrDefault();

            if (record == null)
            {
                _logger.Log(_runtimeOperationIdProvider.OperationId, "Entity not found", LPSLoggingLevel.Error);
                throw new RpcException(new Status(StatusCode.NotFound, "Entity not found"));
            }

            return Task.FromResult(new EntityDiscoveryRecordResponse
            {
                FullyQualifiedName = record.FullyQualifiedName,
                RoundId = record.RoundId.ToString(),
                IterationId = record.IterationId.ToString(),
                RequestId = record.RequestId.ToString(),
                Node = new Node
                {
                    Name = record.Node.Metadata.NodeName,
                    NodeIP = record.Node.Metadata.NodeIP
                }
            });
        }
    }
}
