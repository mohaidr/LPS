using Grpc.Net.Client;
using LPS.Infrastructure.GRPCClients.Factory;
using LPS.Protos.Shared;

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcHostMetricsClient : HostMetricsProtoService.HostMetricsProtoServiceClient, IGRPCClient, ISelfGRPCClient
    {
        private readonly GrpcChannel _channel;

        private static GrpcChannel CreateChannel(string address, out GrpcChannel channel)
        {
            channel = GrpcChannel.ForAddress(address);
            return channel;
        }

        private GrpcHostMetricsClient(string grpcAddress) : base(CreateChannel(grpcAddress, out var ch))
        {
            _channel = ch;
        }

        public static IGRPCClient Create(string grpcAddress)
        {
            return new GrpcHostMetricsClient(grpcAddress);
        }
    }
}
