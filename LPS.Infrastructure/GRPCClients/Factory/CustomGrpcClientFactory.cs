using Grpc.Core;
using Grpc.Net.Client;
using LPS.Infrastructure.Nodes;
using System;
using System.Collections.Concurrent;

namespace LPS.Infrastructure.GRPCClients.Factory
{
    public class CustomGrpcClientFactory : ICustomGrpcClientFactory
    {
        private readonly ConcurrentDictionary<(Type, string), IGRPCClient> _instances = new();
        private readonly IClusterConfiguration _clusterConfiguration;

        public CustomGrpcClientFactory(IClusterConfiguration clusterConfiguration)
        {
            _clusterConfiguration = clusterConfiguration;
        }

        public TClient GetClient<TClient>(string host)
            where TClient : ISelfGRPCClient, IGRPCClient
        {
            var key = (typeof(TClient), host);

            return (TClient)_instances.GetOrAdd(key, _ =>
            {
                var fullAddress = $"http://{host}:{_clusterConfiguration.GRPCPort}";
                return TClient.Create(fullAddress);
            });
        }

        public TClient GetClient<TClient>(string host, Func<string, TClient> factory)
            where TClient : IGRPCClient
        {
            var key = (typeof(TClient), host);

            return (TClient)_instances.GetOrAdd(key, _ =>
            {
                var fullAddress = $"http://{host}:{_clusterConfiguration.GRPCPort}";
                return factory(fullAddress);
            });
        }
    }
}
