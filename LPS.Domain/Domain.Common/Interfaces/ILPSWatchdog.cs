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
        Hot,
        Unkown
    }
    public interface ILPSWatchdog
    {
        /// <summary>
        /// Block the execution until the resources cool down.
        /// </summary>
        /// <param name="hostName">The host name.</param>
        /// <returns>Returns Cool if the resources cools down successfully and Unkown if exception happens during the cooldown process.</returns>
        public Task<ResourceState> BalanceAsync(string hostName, ICancellationTokenWrapper cancellationTokenWrapper);
    }
}
