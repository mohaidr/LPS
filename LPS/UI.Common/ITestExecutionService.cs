using LPS.UI.Core.LPSCommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPS.UI.Common
{
    public interface ITestExecutionService
    {
        bool HasFailedIterations { get; }
        Task ExecuteAsync(TestRunParameters parameters);
        Task<bool> PrepareAsync(TestRunParameters parameters);
        Task ExecutePreparedAsync(TestRunParameters parameters);
        Task PersistMetricsAsync(CancellationToken token);
    }
}
