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
        public int WorkerRegistrationPort { get;}
        public int ExpectedNumberOfWorkers { get;}

        private ClusterConfiguration()
        {
            MasterNodeIP = "127.0.0.1";
            WorkerRegistrationPort = 9009;
            ExpectedNumberOfWorkers = 1;
        }

        public ClusterConfiguration(string masterNodeIP, int workerRegistrationPort, int expectedNumberOfWorkers)
        {
            MasterNodeIP = masterNodeIP;
            WorkerRegistrationPort = workerRegistrationPort;
            ExpectedNumberOfWorkers = expectedNumberOfWorkers;
        }

        public static ClusterConfiguration GetDefaultInstance()
        {
            return new ClusterConfiguration();
        }
    }
}
