using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface IIterationFailureEvaluator
    {
        Task<bool> EvaluateFailureAsync(HttpIteration iteration, CancellationToken token = default);
    }
}
