using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface ITerminationCheckerService
    {
        public Task<bool> IsTerminationRequiredAsync(Iteration iteration, CancellationToken token = default); 
    }
}
