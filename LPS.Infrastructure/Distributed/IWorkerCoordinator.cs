using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using LPS.Protos.Shared;

namespace LPS.Infrastructure.Distributed;

public interface IWorkerCoordinator
{
    Task ConnectAsync(IAsyncStreamReader<WorkerUpdate> updates, IServerStreamWriter<WorkerInstruction> instructions, CancellationToken token);
}