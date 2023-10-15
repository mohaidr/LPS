using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.Domain.Common
{
    public enum ResourceState { 
        Cool,
        Cooling,
        Hot
    }
    public interface ILPSResourceTracker
    {
        public ResourceState Balance();
    }
}
