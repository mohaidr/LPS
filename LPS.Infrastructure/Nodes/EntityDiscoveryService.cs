#nullable enable

using LPS.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public class EntityDiscoveryService: IEntityDiscoveryService
    {
        ILogger _logger;
        IRuntimeOperationIdProvider _operationIdProvider;
        INodeMetadata _nodeMetaData;
        public EntityDiscoveryService(
            INodeMetadata nodeMetaData,
            ILogger logger, 
            IRuntimeOperationIdProvider operationIdProvider = null)
        {
            _entityDiscoveryRecords = new List<EntityDiscoveryRecord>();
            _logger = logger;
            _operationIdProvider = operationIdProvider;
            _nodeMetaData = nodeMetaData;
        }

        private readonly ICollection<EntityDiscoveryRecord> _entityDiscoveryRecords;

        public void AddEntityDiscoveryRecord(string fullyQualifiedName, Guid roundId, Guid iterationId, Guid requestId, INode node)
        {
            var record = new EntityDiscoveryRecord(fullyQualifiedName, roundId, iterationId, requestId, node);

            if (!_entityDiscoveryRecords.Any(record=> record.FullyQualifiedName == fullyQualifiedName && record.Node.Metadata.NodeName == node.Metadata.NodeName))
            {
                if (node.Metadata.NodeType != NodeType.Master && _entityDiscoveryRecords.Any(record => record.FullyQualifiedName == fullyQualifiedName && record.Node.Metadata.NodeType == NodeType.Master))
                {
                    _entityDiscoveryRecords.Add(record);
                }
                else if(node.Metadata.NodeName == _nodeMetaData.NodeName)
                {
                    _entityDiscoveryRecords.Add(record);
                }

                _logger.Log(_operationIdProvider.OperationId, $"entity with FQDN '{fullyQualifiedName}' and request Id '{requestId}' has been added to discovery record", LPSLoggingLevel.Verbose);
            }
            else {
                _logger.Log(_operationIdProvider.OperationId, $"entity with FQDN '{fullyQualifiedName}' and request Id '{requestId}' already exists", LPSLoggingLevel.Warning);
            }
        }
        public ICollection<IEntityDiscoveryRecord>? Discover(Func<IEntityDiscoveryRecord, bool> predict)
        {
            return _entityDiscoveryRecords.Where(predict).ToList();
        }
    }

    public record EntityDiscoveryRecord: IEntityDiscoveryRecord
    {
        public EntityDiscoveryRecord(string fullyQualifiedName, Guid roundId, Guid iterationId, Guid requestId, INode node)
        {
            FullyQualifiedName = fullyQualifiedName;
            RoundId = roundId;
            IterationId = iterationId;
            RequestId = requestId;
            Node = node;
        }
        public string FullyQualifiedName { get; }
        public Guid RoundId { get; }
        public Guid IterationId { get; }
        public Guid RequestId { get; }
        public INode Node { get; }
    }
}
