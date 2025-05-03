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
using System.Net;

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcEntityDiscoveryClient : EntityDiscoveryProtoService.EntityDiscoveryProtoServiceClient, ISelfGRPCClient, IGRPCClient
    {
        private readonly GrpcChannel _channel;

        private static GrpcChannel CreateChannel(string address, out GrpcChannel channel)
        {
            channel = GrpcChannel.ForAddress(address);
            return channel;
        }

        private GrpcEntityDiscoveryClient(string grpcAddress) : base(CreateChannel(grpcAddress, out var ch))
        {
            _channel = ch;
        }

        public static IGRPCClient Create(string grpcAddress)
        {
            return new GrpcEntityDiscoveryClient(grpcAddress);
        }
    }
}
