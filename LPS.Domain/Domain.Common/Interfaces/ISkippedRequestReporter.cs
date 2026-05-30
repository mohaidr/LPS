using System;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.Common.Interfaces
{
    public interface ISkippedRequestReporter
    {
        ValueTask<bool> ReportSkippedRequestAsync(Guid requestId, CancellationToken token = default);
    }
}
