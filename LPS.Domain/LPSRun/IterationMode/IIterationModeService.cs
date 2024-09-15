using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSRun.IterationMode
{
    internal interface IIterationModeService
    {
        Task<int> ExecuteAsync(CancellationToken cancellationToken);
    }
}
