using System.Threading;
using System.Threading.Tasks;

namespace LPS.Common.Interfaces
{
    public interface IMetricsDispatcher
    {
        Task CompleteAsync(CancellationToken token);
    }
}