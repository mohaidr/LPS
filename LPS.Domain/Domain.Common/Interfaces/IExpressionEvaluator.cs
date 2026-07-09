using System.Threading.Tasks;
using System.Threading;

namespace LPS.Domain.Domain.Common.Interfaces
{
    public interface IExpressionEvaluator
    {
        public Task<bool> EvaluateAsync(string expression, string sessionId, CancellationToken token);

        /// <summary>
        /// Evaluates an expression that is ALREADY fully resolved (no placeholder substitution is
        /// performed). Used by callers such as $find that inline their own values (e.g. item fields)
        /// and must not have the string re-scanned for '$' placeholders.
        /// </summary>
        public Task<bool> EvaluateResolvedAsync(string resolvedExpression, CancellationToken token);

        /// <summary>
        /// Resolves an expression for its side-effects only (e.g. methods like $find that store
        /// variables). The resolved value is discarded. Used by "before:" pre-execution hooks.
        /// </summary>
        public Task RunAsync(string expression, string sessionId, CancellationToken token);
    }
}