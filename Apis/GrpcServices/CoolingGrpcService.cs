using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;
using LPS.Protos.Shared;
using NodeType = LPS.Infrastructure.Nodes.NodeType;

namespace Apis.Services;

public sealed class CoolingGrpcService(ICoolingTracker tracker, INodeMetadata node) : CoolingProtoService.CoolingProtoServiceBase
{
    public override Task<CoolingReportResponse> Report(CoolingReportRequest request, ServerCallContext context)
    {
        if (node.NodeType != NodeType.Master)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Cooling reports must be sent to the master."));

        var updates = new List<CoolingUpdate>();
        foreach (var update in request.Updates)
        {
            if (string.IsNullOrWhiteSpace(update.NodeId) || string.IsNullOrWhiteSpace(update.HostName) ||
                update.Source != "Watchdog" || update.Start == null || update.End == null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid cooling interval or node identity."));

            DateTime start;
            DateTime end;
            try
            {
                start = update.Start.ToDateTime();
                end = update.End.ToDateTime();
            }
            catch (InvalidOperationException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid cooling timestamps."));
            }
            if (end < start)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Cooling end must not precede its start."));

            updates.Add(new CoolingUpdate(update.HostName,
                new CoolingPeriod(update.Source, start, end, update.NodeId, update.MachineName, update.Reason), update.IsActive));
        }
        foreach (var update in updates) tracker.ApplyUpdate(update);
        return Task.FromResult(new CoolingReportResponse { Success = true });
    }
}