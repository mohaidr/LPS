using System;
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
                
                // IMPORTANT: We compile as boolean; non-boolean expressions will throw with a clear message.
                var fleeExpr = _ctx.CompileGeneric<bool>(resolved);

                bool result = fleeExpr.Evaluate();
                if (result)
                {
                    await _logger.LogAsync(
                        $"The expression evaluation ({resolved}) failed." +
                        $"Node Details (Name: {_nodeMetadata.NodeName}, IP: {_nodeMetadata.NodeIP})",
                        LPSLoggingLevel.Warning).ConfigureAwait(false);

                    return true;
                }

                return false;
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

        /// <summary>
        /// Converts C#-style comparison operators to Flee-compatible operators.
        /// == becomes = and != becomes <>
        /// </summary>
        private static string NormalizeOperators(string expression)
        {
            if (string.IsNullOrEmpty(expression))
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
