using Grpc.Core;
using LPS.Infrastructure.Distributed;
using LPS.Protos.Shared;

namespace Apis.GrpcServices;

public sealed class WorkerControlGrpcService(IWorkerCoordinator? coordinator = null) : WorkerControl.WorkerControlBase
{
    public override Task Connect(IAsyncStreamReader<WorkerUpdate> requestStream, IServerStreamWriter<WorkerInstruction> responseStream, ServerCallContext context)
        => coordinator?.ConnectAsync(requestStream, responseStream, context.CancellationToken)
            ?? throw new RpcException(new Status(StatusCode.Unimplemented, "This process is not a worker-control master."));
}