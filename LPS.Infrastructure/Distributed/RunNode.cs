using System.Threading;
using System.Threading.Tasks;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using NodeStatus = LPS.Infrastructure.Nodes.NodeStatus;

namespace LPS.Infrastructure.Distributed;

public sealed class RunNode(INodeMetadata metadata) : INode
{
    private int _status = (int)NodeStatus.Created;
    public INodeMetadata Metadata { get; } = metadata;
    public NodeStatus NodeStatus => (NodeStatus)Volatile.Read(ref _status);
    public bool IsActive() => NodeStatus is NodeStatus.Created or NodeStatus.Ready or NodeStatus.Running;
    public bool IsInActive() => !IsActive();
    public ValueTask<SetNodeStatusResponse> SetNodeStatus(NodeStatus nodeStatus)
    {
        Volatile.Write(ref _status, (int)nodeStatus);
        return ValueTask.FromResult(new SetNodeStatusResponse { Success = true });
    }
}