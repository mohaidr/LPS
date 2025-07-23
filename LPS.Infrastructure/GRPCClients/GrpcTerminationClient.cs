using Grpc.Net.Client;
using LPS.Protos.Shared;
using LPS.Infrastructure.GRPCClients.Factory;
using System.Threading.Tasks;
using System.Threading;

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcIterationTerminationServiceClient :
        IterationTerminationService.IterationTerminationServiceClient,
        IGRPCClient,
        ISelfGRPCClient
    {
        private readonly GrpcChannel _channel;

        private static GrpcChannel CreateChannel(string address, out GrpcChannel channel)
        {
            channel = GrpcChannel.ForAddress(address);
            return channel;
        }

        private GrpcIterationTerminationServiceClient(string grpcAddress)
            : base(CreateChannel(grpcAddress, out var ch))
        {
            _channel = ch;
        }

        public async Task<bool> IsTerminatedAsync(string fullyQualifiedName, CancellationToken token = default)
        {
            var request = new IsTerminatedByFQDNRequest
            {
                FullyQualifiedName = fullyQualifiedName
            };

            var response = await IsTerminatedAsync(request, cancellationToken: token);
            return response.IsTerminated;
        }

        public static IGRPCClient Create(string grpcAddress)
        {
            return new GrpcIterationTerminationServiceClient(grpcAddress);
        }
    }
}
