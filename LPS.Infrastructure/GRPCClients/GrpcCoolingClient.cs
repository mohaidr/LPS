using Grpc.Net.Client;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Protos.Shared;

namespace LPS.Infrastructure.GRPCClients;

public class GrpcCoolingClient : CoolingProtoService.CoolingProtoServiceClient, IGRPCClient, ISelfGRPCClient
{
    private GrpcCoolingClient(string address) : base(GrpcChannel.ForAddress(address)) { }

    public static IGRPCClient Create(string grpcAddress) => new GrpcCoolingClient(grpcAddress);
}