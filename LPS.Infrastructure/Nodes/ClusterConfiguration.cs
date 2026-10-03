using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public class ClusterConfiguration : IClusterConfiguration
    {
        public string MasterNodeIP { get; }
        public int MasterNodePort { get; }
        public int GRPCPort => MasterNodePort;
        public int ExpectedNumberOfWorkers { get;}
        public bool MasterNodeIsWorker { get; }

        private ClusterConfiguration(string masterNodeIp, int defaultGrpcPort)
        {
            MasterNodeIP = masterNodeIp;
            MasterNodePort = defaultGrpcPort;
            ExpectedNumberOfWorkers = 0;
            MasterNodeIsWorker = true;
        }

        public ClusterConfiguration(string masterNodeIP, int gRPCPort, bool masterIsWorker, int expectedNumberOfWorkers)
        {
            MasterNodeIP = masterNodeIP;
            MasterNodePort = gRPCPort;
            ExpectedNumberOfWorkers = expectedNumberOfWorkers;
            MasterNodeIsWorker = masterIsWorker;
        }

        public static ClusterConfiguration GetDefaultInstance(string masterNodeIp, int defaultGrpcPort)
        {
            return new ClusterConfiguration(masterNodeIp, defaultGrpcPort);
        }
    }
}
