using System.Threading.Tasks;
using System.Threading;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface IIfEvaluator
    {
        public Task<bool> EvaluateAsync(string expression, string sessionId, CancellationToken token);
    }
}