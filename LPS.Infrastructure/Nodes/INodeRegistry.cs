using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LPS.Infrastructure.Nodes
{
    public interface INodeRegistry
    {
        void RegisterNode(INode node);
        void UnregisterNode(INode node);

        IEnumerable<INode> Query(Func<INode, bool> predicate);

        // Existing getters (can throw if not found)
        INode GetMasterNode();
        INode GetLocalNode();

        // New TryGet-style APIs (return false if not available)
        bool TryGetMasterNode([NotNullWhen(true)] out INode? masterNode);
        bool TryGetLocalNode([NotNullWhen(true)] out INode? localNode);

        IEnumerable<INode> GetNeighborNodes();

        IEnumerable<INode> GetActiveNodes();
        IEnumerable<INode> GetInActiveNodes();
    }
}
