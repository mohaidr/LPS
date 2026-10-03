using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using LPS.Infrastructure.Nodes;

namespace LPS.Infrastructure.Distributed;

public sealed class RunNodeRegistry : INodeRegistry
{
    private readonly ConcurrentDictionary<string, INode> _nodes = new(StringComparer.Ordinal);
    private readonly string _localAddress;
    private readonly string _masterAddress;

    public RunNodeRegistry(ClusterRunSettings settings, INodeMetadata localMetadata)
    {
        _localAddress = settings.NodeAddress;
        _masterAddress = settings.MasterAddress;
        RegisterNode(new RunNode(localMetadata));
        if (settings.Role == NodeType.Worker)
        {
            var cluster = new ClusterConfiguration(settings.MasterAddress, new Uri(settings.MasterAddress).Port, false, 0);
            RegisterNode(new RunNode(new NodeMetadata(cluster, "master", settings.MasterAddress, "", "", "", "", 0, "", [], [])));
        }
    }

    public void RegisterNode(INode node) => _nodes.TryAdd(node.Metadata.NodeIP, node);
    public void UnregisterNode(INode node) => _nodes.TryRemove(new KeyValuePair<string, INode>(node.Metadata.NodeIP, node));
    public IEnumerable<INode> Query(Func<INode, bool> predicate) => _nodes.Values.Where(predicate).ToArray();
    public INode GetLocalNode() => TryGetLocalNode(out var node) ? node : throw new InvalidOperationException("Local run node is unavailable.");
    public INode GetMasterNode() => TryGetMasterNode(out var node) ? node : throw new InvalidOperationException("Master run node is unavailable.");
    public bool TryGetLocalNode([NotNullWhen(true)] out INode? node) => _nodes.TryGetValue(_localAddress, out node);
    public bool TryGetMasterNode([NotNullWhen(true)] out INode? node) => _nodes.TryGetValue(_masterAddress, out node);
    public IEnumerable<INode> GetNeighborNodes() => Query(node => node.Metadata.NodeIP != _localAddress);
    public IEnumerable<INode> GetActiveNodes() => Query(node => node.IsActive());
    public IEnumerable<INode> GetInActiveNodes() => Query(node => node.IsInActive());
}