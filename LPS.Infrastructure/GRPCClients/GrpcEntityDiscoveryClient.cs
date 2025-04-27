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
    public class GrpcEntityDiscoveryClient : EntityDiscoveryProtoService.EntityDiscoveryProtoServiceClient, IGRPCClient, ISelfGRPCClient
    {
        private readonly EntityDiscoveryProtoService.EntityDiscoveryProtoServiceClient _client;
        public GrpcEntityDiscoveryClient() 
        {
        }
        private GrpcEntityDiscoveryClient(string grpcAddress) : base(GrpcChannel.ForAddress(grpcAddress))
        {
            var channel = Grpc.Net.Client.GrpcChannel.ForAddress(grpcAddress);
            _client = new EntityDiscoveryProtoService.EntityDiscoveryProtoServiceClient(channel);
        }

        public ISelfGRPCClient GetClient(string grpcAddress)
        {
           return new GrpcEntityDiscoveryClient(grpcAddress);
        }
    }
}
