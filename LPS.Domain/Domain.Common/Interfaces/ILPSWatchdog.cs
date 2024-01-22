using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public enum ResourceState { 
        Cool,
        Cooling,
        Hot
    }
    public interface ILPSWatchdog
    {
        public Task<ResourceState> Balance(string source);
    }
}
