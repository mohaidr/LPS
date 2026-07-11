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
        /// Evaluates an expression and returns its result as <typeparamref name="T"/> (e.g. string,
        /// int, double, decimal, bool). Placeholders are resolved first, exactly like
        /// <see cref="EvaluateAsync"/>. Returns <paramref name="defaultValue"/> when the expression is
        /// empty or cannot be evaluated/converted to <typeparamref name="T"/>.
        /// </summary>
        public Task<T> EvaluateValueAsync<T>(string expression, string sessionId, CancellationToken token, T defaultValue = default);

        /// <summary>
        /// Resolves an expression for its side-effects only (e.g. methods like $find that store
        /// variables). The resolved value is discarded. Used by "before:" pre-execution hooks.
        /// </summary>
        public Task RunAsync(string expression, string sessionId, CancellationToken token);
    }
}