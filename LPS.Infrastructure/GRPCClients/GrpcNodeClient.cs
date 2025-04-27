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
using LPS.Infrastructure.Nodes;

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcNodeClient : NodeService.NodeServiceClient, IGRPCClient, ISelfGRPCClient
    {
        private readonly NodeService.NodeServiceClient _client;
        public GrpcNodeClient()
        {
        }
        private GrpcNodeClient(string grpcAddress): base(GrpcChannel.ForAddress(grpcAddress))
        {
            var channel = Grpc.Net.Client.GrpcChannel.ForAddress(grpcAddress);
            _client = new NodeService.NodeServiceClient(channel);
        }

        public ISelfGRPCClient GetClient(string grpcAddress)
        {
           return new GrpcNodeClient(grpcAddress);
        }
    }
}
