using System.Threading.Tasks;
using System.Threading;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface IIfEvaluator
    {
        public Task<bool> EvaluateAsync(string expression, string sessionId, CancellationToken token);

        /// <summary>
        /// Resolves an expression for its side-effects only (e.g. methods like $find that store
        /// variables). The resolved value is discarded. Used by "before:" pre-execution hooks.
        /// </summary>
        public Task RunAsync(string expression, string sessionId, CancellationToken token);
    }
}