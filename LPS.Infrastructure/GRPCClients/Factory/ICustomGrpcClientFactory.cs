using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.GRPCClients.Factory
{
    public interface ICustomGrpcClientFactory
    {
        TClient GetClient<TClient>(string grpcAddress) where TClient : ISelfGRPCClient, IGRPCClient;
        TClient GetClient<TClient>(string grpcAddress, Func<string, TClient> factory)
    where TClient : IGRPCClient;

    }

}
