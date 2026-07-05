using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flee.PublicTypes;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.Nodes;

namespace LPS.Infrastructure.Skip
{
    public class IfEvaluator : IIfEvaluator
    {
        private readonly IPlaceholderResolverService _placeholderResolver;
        private readonly ILogger _logger;
        private readonly INodeMetadata _nodeMetadata;
        ExpressionContext _ctx;

        // Caches the boolean RESULT keyed by the normalized, RESOLVED expression. Safe because, after
        // resolution, the string is pure literals (deterministic) and placeholder side-effects have
        // already happened. Skips both the Flee compile and evaluate on a hit.
        private static readonly ConcurrentDictionary<string, bool> ResultCache = new(StringComparer.Ordinal);
        private const int MaxResultCacheSize = 1024;
        // Resolved strings longer than this are almost certainly high-cardinality (embedded ids,
        // bodies) and would only churn the cache — evaluate them without caching.
        private const int MaxCacheableKeyLength = 512;
        public IfEvaluator(
            IPlaceholderResolverService placeholderResolver,
            INodeMetadata nodeMetadata,
            IRuntimeOperationIdProvider runtimeOperationIdProvider, // kept to match your ctor signature
            ILogger logger)
        {
            _placeholderResolver = placeholderResolver;
            _logger = logger;
            _nodeMetadata = nodeMetadata;
            // 2) Prepare Flee context
            _ctx = new ExpressionContext();

            // (optional but recommended) invariant parsing for numbers/decimals
            _ctx.Options.ParseCulture = CultureInfo.InvariantCulture;

            // If expressions may need System.Math:
            _ctx.Imports.AddType(typeof(Math));

            // Enable overflow-checked arithmetic:
            _ctx.Options.Checked = true;

            //Make the type System.StringComparison available inside expressions
            _ctx.Imports.AddType(typeof(System.StringComparison));

            _ctx.Imports.AddType(typeof(string), "String");   // enables String.IsNullOrEmpty(...)
            _ctx.Imports.AddType(typeof(Convert), "Convert");  // enables Convert.ToInt32(...), etc.

        }

        // NOTE:
        // Flee is synchronous. We keep the async signature to the interface and logging.
        // Expressions MUST resolve to a boolean result.
        public async Task<bool> EvaluateAsync(string expression, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            string resolved = string.Empty;
            try
            {
                // 1) Resolve placeholders first (your existing flow)
                resolved = await _placeholderResolver
                    .ResolvePlaceholdersAsync<string>(expression, sessionId, token)
                    .ConfigureAwait(false);
                // 2) Normalize operators: convert C#-style to Flee-compatible
                resolved = NormalizeOperators(resolved);

                // Result cache (keyed by the normalized RESOLVED string). Resolution already ran above,
                // so any placeholder side-effects are preserved; only the pure compile+evaluate is skipped.
                var cacheable = resolved.Length <= MaxCacheableKeyLength;
                if (cacheable && ResultCache.TryGetValue(resolved, out var cachedResult))
                {
                    return cachedResult;
                }

                // IMPORTANT: We compile as boolean; non-boolean expressions will throw with a clear message.
                var fleeExpr = _ctx.CompileGeneric<bool>(resolved);

                bool result = fleeExpr.Evaluate();

                if (cacheable)
                {
                    ResultCache[resolved] = result;
                    TrimResultCacheIfNeeded();
                }

                return result;
            }
            catch (Exception ex)
            {
                // Mirror your diagnostic style, updated to reflect Flee behavior.
                var message =
                    "Failed to evaluate the expression.\r\n" +
                    "\r\n" +
                    $"The Skip condition: {expression}\r\n" +
                    $"Resolved to: {resolved}\r\n" +
                    "\r\n" +
                    "Why?\r\n" +
                    "\t1- If you use a placeholder, make sure it resolves properly.\r\n" +
                    "\t2- Ensure string values are quoted (e.g., \"OK\").\r\n" +
                    "\t3- Flee requires the expression to be boolean (true/false). Use comparisons or ternary to return bool.\r\n" +
                    "\r\n" +
                    $"Exception: {ex}";
                await _logger.LogAsync(message, LPSLoggingLevel.Error).ConfigureAwait(false);
                // treat error as 'skip = false' as we can't decide to skip or not.
                return false;
            }
        }

        public async Task RunAsync(string expression, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;

            try
            {
                // Resolve for side-effects only (e.g. $find(..., variable=x)); the value is discarded.
                await _placeholderResolver
                    .ResolvePlaceholdersAsync<string>(expression, sessionId, token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(
                    $"Failed to run 'before' expression '{expression}'. Exception: {ex}",
                    LPSLoggingLevel.Error).ConfigureAwait(false);
            }
        }

        private static void TrimResultCacheIfNeeded()
        {
            // Partial eviction of arbitrary keys instead of Clear(): a full wipe caused every
            // in-flight evaluation to recompile at once. Runs after the insert, so the bound is
            // self-correcting even when concurrent adds overshoot it.
            foreach (var key in ResultCache.Keys)
            {
                if (ResultCache.Count <= MaxResultCacheSize) break;
                ResultCache.TryRemove(key, out _);
            }
        }

        private static readonly char[] OperatorChars = { '!', '=', '&', '|' };

        /// <summary>
        /// Converts C#-style comparison operators to Flee-compatible operators.
        /// == becomes = and != becomes <>
        /// KNOWN ISSUE: the replacements are not quote-aware, so substituted data values that
        /// contain these characters inside string literals (e.g. "R&D") are rewritten too.
        /// </summary>
        private static string NormalizeOperators(string expression)
        {
            if (string.IsNullOrEmpty(expression) || expression.IndexOfAny(OperatorChars) < 0)
                return expression;

            // Replace != with <> first (before replacing ==)
            // to avoid partial replacements
            expression = expression.Replace("!=", "<>");
            expression = expression.Replace("==", "=");
            expression = expression.Replace("&&", "and");
            expression = expression.Replace("&", "and");
            expression = expression.Replace("|", "or");

            return expression;
        }
    }
}
