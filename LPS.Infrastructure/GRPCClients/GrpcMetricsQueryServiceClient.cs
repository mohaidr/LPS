using Grpc.Net.Client;
using Grpc.Core;
using LPS.Protos.Shared;
using LPS.Infrastructure.GRPCClients.Factory;
using System.Threading.Tasks;
using System.Threading;

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcMetricsQueryServiceClient : MetricsQueryService.MetricsQueryServiceClient, IGRPCClient, ISelfGRPCClient
    {
        private readonly GrpcChannel _channel;

        private static GrpcChannel CreateChannel(string address, out GrpcChannel channel)
        {
            channel = GrpcChannel.ForAddress(address);
            return channel;
        }

        private GrpcMetricsQueryServiceClient(string grpcAddress) : base(CreateChannel(grpcAddress, out var ch))
        {
            _channel = ch;
        }

        public async Task<DurationMetricSearchResponse> GetDurationAsync(string fqdn, CancellationToken token = default)
        {
            var request = new MetricRequest
            {
                FullyQualifiedName = fqdn
            };
            return await GetDurationMetricsAsync(request, cancellationToken: token);
        }

        public async Task<DataTransmissionMetricSearchResponse> GetDataTransmissionAsync(string fqdn, CancellationToken token = default)
        {
            var request = new MetricRequest
            {
                FullyQualifiedName = fqdn
            };
            return await GetDataTransmissionMetricsAsync(request, cancellationToken: token);
        }

        public async Task<ThroughputMetricSearchResponse> GetThroughputAsync(string fqdn, CancellationToken token = default)
        {
            var request = new MetricRequest
            {
                FullyQualifiedName = fqdn
            };
            return await GetThroughputMetricsAsync(request, cancellationToken: token);
        }

        public async Task<ResponseCodeMetricSearchResponse> GetResponseCodesAsync(string fqdn, CancellationToken token = default)
        {
            var request = new MetricRequest
            {
                FullyQualifiedName = fqdn
            };
            return await GetResponseCodeMetricsAsync(request, cancellationToken: token);
        }

        public static IGRPCClient Create(string grpcAddress)
        {
            return new GrpcMetricsQueryServiceClient(grpcAddress);
        }
    }
}
