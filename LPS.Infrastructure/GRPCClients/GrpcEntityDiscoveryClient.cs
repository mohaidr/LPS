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
    public class GrpcEntityDiscoveryClient : EntityDiscoveryProtoService.EntityDiscoveryProtoServiceClient, ISelfGRPCClient, IGRPCClient
    {
        private GrpcEntityDiscoveryClient(string grpcAddress)
        {
        }

        public static IGRPCClient Create(string grpcAddress)
        {
            return new GrpcEntityDiscoveryClient(grpcAddress);
        }
    }
}
