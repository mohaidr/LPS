using Grpc.Net.Client;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.GRPCExtensions;
using LPS.Protos.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using LPS.Infrastructure.GRPCClients.Factory;
using System.Net.Sockets;
using System;
using Grpc.Core;

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcMonitorClient : MonitorService.MonitorServiceClient, IGRPCClient, ISelfGRPCClient
    {
        private readonly GrpcChannel _channel;

        private static GrpcChannel CreateChannel(string address, out GrpcChannel channel)
        {
            channel = GrpcChannel.ForAddress(address);
            return channel;
        }

        private GrpcMonitorClient(string grpcAddress) : base(CreateChannel(grpcAddress, out var ch))
        {
            _channel = ch;
        }

        public async Task<List<Domain.Domain.Common.Enums.ExecutionStatus>> QueryStatusesAsync(string fqdn, CancellationToken token = default)
        {
            var request = new StatusQueryRequest { FullyQualifiedName = fqdn };
            var response = await base.QueryIterationStatusesAsync(request, cancellationToken: token);
            return response.Statuses.Select(s => s.ToLocal()).ToList();
        }

        public async Task<bool> MonitorAsync(string fqdn, CancellationToken token = default) // Optional overloaded method if that adds a benefit like removing code redunduncy. This approach is also composition over inheritance
        {
            try
            {
                var request = new MonitorRequest { FullyQualifiedName = fqdn };
                var response = await base.MonitorAsync(request, cancellationToken: token);

                if (!response.Success)
                {
                    throw new InvalidOperationException($"Monitor call failed: {response.Message}");
                }

                return true;
            }
            catch (RpcException rpcEx)
            {
                // Optional: handle gRPC-specific errors
                throw new InvalidOperationException($"gRPC Monitor call failed: {rpcEx.Status.Detail}", rpcEx);
            }
        }

        public static IGRPCClient Create(string grpcAddress)
        {
           return new GrpcMonitorClient(grpcAddress);
        }
    }
}
