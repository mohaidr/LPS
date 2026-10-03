using LPS.Infrastructure.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common.Options
{
    public class ClusterConfigurationOptions
    {
        private int? _masterNodePort;
        public string? MasterNodeIP { get; set; }
        public int? MasterNodePort { get => _masterNodePort ?? GRPCPort; set => _masterNodePort = value; }
        public int? GRPCPort { get; set; }
        public int? ExpectedNumberOfWorkers { get; set; } = 0;
        public bool? MasterNodeIsWorker { get; set; }
    }
}
