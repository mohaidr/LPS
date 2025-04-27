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
    public class GrpcMetricsClient : MetricsProtoService.MetricsProtoServiceClient, IGRPCClient, ISelfGRPCClient
    {
        private readonly MetricsProtoService.MetricsProtoServiceClient _client;
        public GrpcMetricsClient() 
        {
        }
        private GrpcMetricsClient(string grpcAddress) : base(GrpcChannel.ForAddress(grpcAddress))
        {
            var channel = Grpc.Net.Client.GrpcChannel.ForAddress(grpcAddress);
            _client = new MetricsProtoService.MetricsProtoServiceClient(channel);
        }

        public ISelfGRPCClient GetClient(string grpcAddress)
        {
           return new GrpcMetricsClient(grpcAddress);
        }
    }
}
