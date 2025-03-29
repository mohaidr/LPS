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

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcStatusClient : IGRPCClient, ISelfGRPCClient
    {
        private readonly StatusMonitorService.StatusMonitorServiceClient _client;

        public GrpcStatusClient() 
        {
        }
        private GrpcStatusClient(string grpcAddress)
        {
            var channel = Grpc.Net.Client.GrpcChannel.ForAddress(grpcAddress);
            _client = new StatusMonitorService.StatusMonitorServiceClient(channel);
        }

        public async Task<List<Domain.Domain.Common.Enums.ExecutionStatus>> QueryStatusesAsync(string fqdn, CancellationToken token = default)
        {

            var request = new StatusQueryRequest { FullyQualifiedName = fqdn };
            var response = await _client.QueryIterationStatusesAsync(request, cancellationToken: token);
            return response.Statuses.Select(s => s.ToLocal()).ToList();
        }



        public ISelfGRPCClient GetClient(string grpcAddress)
        {
           return new GrpcStatusClient(grpcAddress);
        }
    }
}
