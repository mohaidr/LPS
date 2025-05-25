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
using Grpc.Net.Client.Configuration;

namespace LPS.Infrastructure.GRPCClients
{
    public class GrpcNodeClient : NodeService.NodeServiceClient, IGRPCClient, ISelfGRPCClient
    {
        private readonly GrpcChannel _channel;

        private static GrpcChannel CreateChannel(string address, out GrpcChannel channel)
        {
            var defaultServiceConfig = new ServiceConfig
            {
                MethodConfigs =
                {
                    new MethodConfig
                    {
                        Names = { MethodName.Default }, // Applies to all methods
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = 2,
                            InitialBackoff = TimeSpan.FromSeconds(1),
                            MaxBackoff = TimeSpan.FromSeconds(5),
                            BackoffMultiplier = 1.5,
                            RetryableStatusCodes = { StatusCode.Unavailable }
                        }
                    }
                }
            };
            channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions 
            { 
                ServiceConfig = defaultServiceConfig
            });
            return channel;
        }

        private GrpcNodeClient(string grpcAddress) : base(CreateChannel(grpcAddress, out var ch))
        {
            _channel = ch;
        }

        public static IGRPCClient Create(string grpcAddress)
        {
            return new GrpcNodeClient(grpcAddress);
        }
    }
}
