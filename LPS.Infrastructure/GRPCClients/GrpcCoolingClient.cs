using Grpc.Net.Client;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Protos.Shared;

namespace LPS.Infrastructure.GRPCClients;

public class GrpcCoolingClient : CoolingProtoService.CoolingProtoServiceClient, IGRPCClient, ISelfGRPCClient
{
    private GrpcCoolingClient(string address) : base(LPS.Infrastructure.Distributed.ClusterGrpcTransport.CreateChannel(address)) { }

    public static IGRPCClient Create(string grpcAddress) => new GrpcCoolingClient(grpcAddress);
}