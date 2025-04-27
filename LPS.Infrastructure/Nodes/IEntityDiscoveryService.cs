#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public interface IEntityDiscoveryService
    {
        void AddEntityDiscoveryRecord(string fullyQualifiedName, Guid roundId, Guid iterationId, Guid requestId, INode node);
        ICollection<IEntityDiscoveryRecord>? Discover(Func<IEntityDiscoveryRecord, bool> predict);

    }

    public interface IEntityDiscoveryRecord
    {
        public string FullyQualifiedName { get; }
        public Guid RoundId { get; }
        public Guid IterationId { get; }
        public Guid RequestId { get; }
        public INode Node { get; }
    }

}
