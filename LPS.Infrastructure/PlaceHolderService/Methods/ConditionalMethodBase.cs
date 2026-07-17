using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// Base for declarative methods that gate a side-effect behind a boolean <c>condition</c>
    /// (e.g. $setvariableif, $readif). Centralises the condition evaluation and the typed variable
    /// storage so the concrete "…if" methods only describe WHAT to store, not HOW.
    /// </summary>
    public abstract class ConditionalMethodBase : MethodBase
    {
        protected readonly Lazy<IExpressionEvaluator> _expressionEvaluator;

        protected ConditionalMethodBase(
            ParameterExtractorService p,
            ILogger l,
            IRuntimeOperationIdProvider op,
            IVariableManager v,
            Lazy<IPlaceholderResolverService> r,
            Lazy<IExpressionEvaluator> expressionEvaluator,
            ISessionManager session)
            : base(p, l, op, v, r, session)
        {
            _expressionEvaluator = expressionEvaluator;
        }

        /// <summary>
        /// Resolves and evaluates the <c>condition</c> argument. The outer resolver has already
        /// substituted any '$' placeholders inside it before dispatch, so it is evaluated WITHOUT a
        /// second placeholder pass (values containing '$' are safe). Returns <c>null</c> when no
        /// condition was supplied.
        /// </summary>
        protected async Task<bool?> TryEvaluateConditionAsync(Dictionary<string, string> args, string sessionId, CancellationToken token)
        {
            var condition = await ResolveArgAsync(args, "condition", string.Empty, sessionId, token);
            if (string.IsNullOrWhiteSpace(condition))
                return null;

            return await _expressionEvaluator.Value.EvaluateResolvedAsync(condition, token);
        }

        /// <summary>
        /// Resolves a named argument (so it can be indirected through a variable, e.g. condition=${myCond}),
        /// falling back to <paramref name="defaultValue"/> when absent.
        /// </summary>
        protected async Task<string> ResolveArgAsync(Dictionary<string, string> args, string key, string defaultValue, string sessionId, CancellationToken token)
        {
            return args.TryGetValue(key, out var raw)
                ? await _resolver.Value.ResolvePlaceholdersAsync<string>(raw, sessionId, token)
                : defaultValue;
        }

        /// <summary>
        /// For a **raw** (unresolved) input such as a `value`/`else` argument: resolves its placeholders
        /// (deferred until this branch is actually taken), then stores it (typed) under
        /// <paramref name="variableName"/>; returns the display string. Use this when the input is a
        /// template that still needs placeholder substitution.
        /// </summary>
        protected async Task<string> ResolveAndStoreAsync(string variableName, string raw, string asType, string sessionId, bool isGlobal, CancellationToken token)
        {
            var resolved = string.IsNullOrEmpty(raw)
                ? string.Empty
                : await _resolver.Value.ResolvePlaceholdersAsync<string>(raw, sessionId, token) ?? string.Empty;

            return await StoreResolvedAsync(variableName, resolved, asType, sessionId, isGlobal, token);
        }

        /// <summary>
        /// For an **already-resolved / final** input such as content read from a file, env var, or another
        /// variable: stores it (typed via <paramref name="asType"/>) under <paramref name="variableName"/>
        /// WITHOUT a further placeholder pass; returns the display string. Use this when the input must NOT
        /// be re-scanned for '$' (e.g. read content that may legitimately contain '$'). When
        /// <paramref name="isGlobal"/> is false the value is stored scoped to <paramref name="sessionId"/>.
        /// </summary>
        protected async Task<string> StoreResolvedAsync(string variableName, string resolved, string asType, string sessionId, bool isGlobal, CancellationToken token)
        {
            var value = BuildValueToken(resolved, asType);
            await StoreTypedVariableAsync(variableName, value, asType, token, isGlobal, sessionId);
            return DisplayString(value);
        }

        protected static string DisplayString(JToken value)
        {
            if (value == null) return string.Empty;
            return value.Type switch
            {
                JTokenType.Array or JTokenType.Object => value.ToString(Newtonsoft.Json.Formatting.None),
                JTokenType.Null => string.Empty,
                _ => value.ToString()
            };
        }
    }
}
