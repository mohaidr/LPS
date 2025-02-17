using LPS.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public class EntityServiceDiscovery
    {
        INodeRegistry _nodeRegistery;
        public EntityServiceDiscovery(INodeRegistry nodeRegistery) { 
           _nodeRegistery = nodeRegistery;
        } 

        private readonly ICollection<EntityDiscoveryRecord> _entityDiscoveryRecords;

        public void AddEntityDiscoveryRecord(string fullyQualifiedName, Guid roundId, Guid iterationId, Guid requestId)
        {
            var record = new EntityDiscoveryRecord(fullyQualifiedName, roundId, iterationId, requestId, _nodeRegistery.FetchLocalNode());
            _entityDiscoveryRecords.Add(record);
        }
        public EntityDiscoveryRecord? DiscoverEntity(string fullyQualifiedName, Guid roundId, Guid iterationId, Guid requestId)
        {
            return _entityDiscoveryRecords.FirstOrDefault(r =>
                r.FullyQualifiedName == fullyQualifiedName &&
                r.RoundId == roundId &&
                r.IterationId == iterationId &&
                r.RequestId == requestId);
        }
    }

    public class EntityDiscoveryRecord
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
