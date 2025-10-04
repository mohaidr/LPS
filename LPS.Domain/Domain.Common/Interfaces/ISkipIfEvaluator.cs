using System.Threading.Tasks;
using System.Threading;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface ISkipIfEvaluator
    {
        public Task<bool> ShouldSkipAsync(string skipIfExpression, string sessionId, CancellationToken token);
    }
}