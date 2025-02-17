using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Infrastructure.Nodes
{
    public interface IClusterConfiguration
    {
        string MasterNodeIP {  get; set; }
        public int WorkerRegistrationPort { get; set; }
        public int ExpectedNumberOfWorkers { get; set; }
    }
}
