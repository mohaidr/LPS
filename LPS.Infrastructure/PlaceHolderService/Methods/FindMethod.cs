using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;
using LPS.Infrastructure.VariableServices.VariableHolders;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// Declarative replacement for a for-loop over a JSON array.
    /// Iterates a JSON array, evaluates a boolean predicate against each element (exposed as the
    /// alias <c>item</c>), projects a value, and stores the result with its proper type.
    ///
    /// Example:
    ///   ${find(source=${response.body}, path=$.data.orders[*],
    ///          where="${item.status}" == "shipped" and ${item.total} > ${threshold},
    ///          select=${item.id}, match=first, variable=shippedOrderId)}
    /// </summary>
    public sealed class FindMethod : MethodBase
    {
        private const string Alias = "item";

        private static readonly Regex BraceAliasRegex = new(@"\$\{\s*" + Alias + @"\b", RegexOptions.Compiled);
        private static readonly Regex StopperAliasRegex = new(@"\$" + Alias + @"\b", RegexOptions.Compiled);

        // Caches parsed JSON keyed by the EXACT resolved source string. Self-invalidating: a different
        // body is a different key, so a changed capture can never return a stale tree. Cached tokens are
        // treated as READ-ONLY (find only reads them via SelectTokens/ToString; JArray-add auto-clones).
        // Hits come from the same body being evaluated by several rules (skip-if, failure, termination,
        // multiple finds) within one iteration. Shared read-only JToken access across sessions is
        // covered by a concurrency stress test; if it ever proves unsafe, DeepClone on hit is the fallback.
        private static readonly ConcurrentDictionary<string, JToken> JsonParseCache = new(StringComparer.Ordinal);
        private const int MaxJsonCacheSize = 32;
        // Bodies above this size are parsed but not cached, to bound worst-case memory retention.
        private const int MaxCacheableJsonLength = 1_000_000;

        private static int _bindingCounter;

        private readonly Lazy<IIfEvaluator> _ifEvaluator;

        public FindMethod(
            ParameterExtractorService p,
            ILogger l,
            IRuntimeOperationIdProvider op,
            IVariableManager v,
            Lazy<IPlaceholderResolverService> r,
            Lazy<IIfEvaluator> ifEvaluator)
            : base(p, l, op, v, r)
        {
            _ifEvaluator = ifEvaluator;
        }

        public override string Name => "find";

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            string bindingName = null;

            try
            {
                // Single scan of the parameters string instead of one full scan per key.
                var args = _params.ParseParameters(parameters);

                // 'source' is taken RAW: the outer resolver pre-resolves any method argument string
                // containing '$' before dispatch, so it is already the substituted body here.
                // Resolving it again would rescan O(body) and mangle bodies containing literal '$'.
                // where/select/default stay raw by design (resolved per element / on fallback).
                var source = args.GetValueOrDefault("source", string.Empty);
                var whereRaw = args.GetValueOrDefault("where", string.Empty);
                var selectRaw = args.GetValueOrDefault("select", string.Empty);
                var defaultRaw = args.GetValueOrDefault("default", string.Empty);

                var path = await ResolveArgAsync(args, "path", string.Empty, sessionId, token);
                var match = (await ResolveArgAsync(args, "match", "first", sessionId, token)).Trim();
                var asType = (await ResolveArgAsync(args, "as", "auto", sessionId, token)).Trim();
                variableName = await ResolveArgAsync(args, "variable", string.Empty, sessionId, token);

                if (!TryGetElements(source, path, out var elements))
                {
                    await _logger.LogAsync(_op.OperationId, "find failed. The source could not be parsed as JSON.", LPSLoggingLevel.Warning, token);
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, match, sessionId, token);
                }

                bindingName = "__find_" + Interlocked.Increment(ref _bindingCounter).ToString(CultureInfo.InvariantCulture);
                var where = RewriteAlias(whereRaw, bindingName);
                var select = RewriteAlias(selectRaw, bindingName);

                var (mode, indexTarget) = ParseMatchMode(match);

                var matches = new List<JToken>();
                var matchCount = 0;
                StringVariableHolder.VMaintainer itemMaintainer = null;

                foreach (var element in elements)
                {
                    token.ThrowIfCancellationRequested();

                    itemMaintainer = await BindItemAsync(itemMaintainer, bindingName, element, token);

                    var isMatch = string.IsNullOrWhiteSpace(where) || await EvaluatePredicateAsync(where, sessionId, token);
                    if (!isMatch) continue;

                    if (mode == MatchMode.Count) { matchCount++; continue; }

                    matches.Add(await ProjectAsync(element, select, sessionId, token));

                    if (mode == MatchMode.First) break;
                    if (mode == MatchMode.Index && matches.Count - 1 == indexTarget) break;
                }

                return mode switch
                {
                    MatchMode.Count => await StoreAndReturnAsync(variableName, new JValue(matchCount), asType, token),
                    MatchMode.All => await StoreAndReturnAsync(variableName, new JArray(matches), asType, token),
                    _ => await StoreSingleAsync(variableName, matches, mode, indexTarget, defaultRaw, asType, sessionId, token)
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"find failed. {ex}", LPSLoggingLevel.Error, token);
                return string.Empty;
            }
            finally
            {
                if (bindingName != null)
                {
                    await _variables.RemoveVariableAsync(bindingName, token);
                }
            }
        }

        private enum MatchMode { First, Last, Index, Count, All }

        private static (MatchMode mode, int index) ParseMatchMode(string match)
        {
            var value = (match ?? "first").Trim().ToLowerInvariant();

            if (value.StartsWith("index"))
            {
                var parts = value.Split(':');
                var index = parts.Length == 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0
                    ? n
                    : 0;
                return (MatchMode.Index, index);
            }

            return value switch
            {
                "last" => (MatchMode.Last, 0),
                "count" => (MatchMode.Count, 0),
                "all" => (MatchMode.All, 0),
                _ => (MatchMode.First, 0)
            };
        }

        private async Task<string> StoreSingleAsync(
            string variableName,
            List<JToken> matches,
            MatchMode mode,
            int indexTarget,
            string defaultRaw,
            string asType,
            string sessionId,
            CancellationToken token)
        {
            JToken picked = mode switch
            {
                MatchMode.Last => matches.Count > 0 ? matches[^1] : null,
                MatchMode.Index => indexTarget < matches.Count ? matches[indexTarget] : null,
                _ => matches.Count > 0 ? matches[0] : null
            };

            if (picked == null)
            {
                return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode.ToString(), sessionId, token);
            }

            return await StoreAndReturnAsync(variableName, picked, asType, token);
        }

        private async Task<string> StoreNoMatchAsync(string variableName, string defaultRaw, string asType, string match, string sessionId, CancellationToken token)
        {
            // "count" with no matches is 0, everything else falls back to the (resolved) default value.
            if (string.Equals(match?.Trim(), "count", StringComparison.OrdinalIgnoreCase))
            {
                return await StoreAndReturnAsync(variableName, new JValue(0), asType, token);
            }

            JToken defaultToken;
            if (string.IsNullOrWhiteSpace(defaultRaw))
            {
                defaultToken = new JValue(string.Empty);
            }
            else
            {
                var resolved = await _resolver.Value.ResolvePlaceholdersAsync<string>(defaultRaw, sessionId, token);
                defaultToken = StringToJToken(resolved);
            }

            return await StoreAndReturnAsync(variableName, defaultToken, asType, token);
        }

        private async Task<string> StoreAndReturnAsync(string variableName, JToken value, string asType, CancellationToken token)
        {
            await StoreTypedVariableAsync(variableName, value, asType, token);
            return DisplayString(value);
        }

        private async Task<string> ResolveArgAsync(Dictionary<string, string> args, string key, string defaultValue, string sessionId, CancellationToken token)
        {
            return args.TryGetValue(key, out var raw)
                ? await _resolver.Value.ResolvePlaceholdersAsync<string>(raw, sessionId, token)
                : defaultValue;
        }

        /// <summary>
        /// Binds the current element under <paramref name="bindingName"/>. The holder is created and
        /// registered once (first element); subsequent elements update the same holder in place via
        /// the maintainer, avoiding a PutAsync (and its collision warning) per element. The element
        /// is passed pre-parsed so predicate/select path accesses skip re-parsing the raw string.
        /// </summary>
        private async Task<StringVariableHolder.VMaintainer> BindItemAsync(
            StringVariableHolder.VMaintainer maintainer, string bindingName, JToken element, CancellationToken token)
        {
            var raw = element.Type is JTokenType.Object or JTokenType.Array
                ? element.ToString(Formatting.None)
                : ScalarString(element);

            var register = maintainer == null;
            maintainer ??= new StringVariableHolder.VMaintainer(_resolver.Value, _logger, _op)
                .WithType(VariableType.JsonString)
                .SetGlobal();

            var holder = await maintainer
                .WithRawValue(raw)
                .WithParsedToken(element)
                .UpdateAsync(token);

            if (register)
            {
                await _variables.SetAsync(bindingName, holder, token);
            }

            return maintainer;
        }

        private async Task<bool> EvaluatePredicateAsync(string where, string sessionId, CancellationToken token)
        {
            try
            {
                return await _ifEvaluator.Value.EvaluateAsync(where, sessionId, token);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"find predicate evaluation failed for '{where}'. {ex.Message}", LPSLoggingLevel.Warning, token);
                return false;
            }
        }

        private async Task<JToken> ProjectAsync(JToken element, string select, string sessionId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(select))
            {
                return element;
            }

            var resolved = await _resolver.Value.ResolvePlaceholdersAsync<string>(select, sessionId, token);
            return StringToJToken(resolved);
        }

        private static bool TryGetElements(string source, string path, out List<JToken> elements)
        {
            elements = new List<JToken>();

            if (!TryParseJson(source, out var root))
            {
                return false;
            }

            IEnumerable<JToken> selected;
            if (string.IsNullOrWhiteSpace(path))
            {
                selected = new[] { root };
            }
            else
            {
                try
                {
                    selected = root.SelectTokens(path.Trim()).ToList();
                }
                catch
                {
                    // Invalid path -> treat as no matches (not a hard failure).
                    return true;
                }
            }

            foreach (var tokenValue in selected)
            {
                if (tokenValue is JArray array)
                {
                    elements.AddRange(array);
                }
                else
                {
                    elements.Add(tokenValue);
                }
            }

            return true;
        }

        private static bool TryParseJson(string value, out JToken root)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(value)) return false;

            // Oversized bodies skip the cache entirely (lookup included — hashing a huge string
            // costs O(len) and such bodies are unlikely to repeat).
            var cacheable = value.Length <= MaxCacheableJsonLength;

            // Cache hit: reuse the already-parsed tree (read-only). Resolution ran before this, so
            // find still executes fully every call; only the JSON parse is skipped.
            if (cacheable && JsonParseCache.TryGetValue(value, out var cached))
            {
                root = cached;
                return true;
            }

            try
            {
                var parsed = JToken.Parse(value);

                // Unwrap a JSON string that itself contains JSON (e.g. "\"[...]\"").
                if (parsed is JValue scalar && scalar.Type == JTokenType.String)
                {
                    var inner = (scalar.Value<string>() ?? string.Empty).Trim();
                    if (inner.StartsWith("[") || inner.StartsWith("{"))
                    {
                        parsed = JToken.Parse(inner);
                    }
                }

                if (cacheable)
                {
                    JsonParseCache[value] = parsed;
                    TrimJsonCacheIfNeeded();
                }
                root = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TrimJsonCacheIfNeeded()
        {
            // Partial eviction of arbitrary keys instead of Clear(): a full wipe forced every rule
            // evaluating the same body to reparse at once. Running after the insert makes the bound
            // self-correcting even when concurrent adds overshoot it.
            foreach (var key in JsonParseCache.Keys)
            {
                if (JsonParseCache.Count <= MaxJsonCacheSize) break;
                JsonParseCache.TryRemove(key, out _);
            }
        }

        private static string RewriteAlias(string expression, string bindingName)
        {
            if (string.IsNullOrEmpty(expression)) return expression;

            expression = BraceAliasRegex.Replace(expression, _ => "${" + bindingName);
            expression = StopperAliasRegex.Replace(expression, _ => "$" + bindingName);
            return expression;
        }

        private static JToken StringToJToken(string value)
        {
            if (value == null) return JValue.CreateNull();

            var trimmed = value.Trim();
            if (trimmed.Length == 0) return new JValue(string.Empty);

            if (trimmed[0] == '{' || trimmed[0] == '[' || (trimmed[0] == '"' && trimmed[^1] == '"'))
            {
                try { return JToken.Parse(trimmed); }
                catch { /* not JSON, fall through */ }
            }

            // Preserve identifiers with leading zeros (ids, zip codes) as strings.
            var leadingZero = trimmed.Length > 1 && trimmed[0] == '0' && char.IsDigit(trimmed[1]);
            if (!leadingZero)
            {
                if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    return new JValue(l);
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return new JValue(d);
            }

            if (bool.TryParse(trimmed, out var b)) return new JValue(b);

            return new JValue(value);
        }

        private static string DisplayString(JToken value)
        {
            if (value == null) return string.Empty;
            return value.Type is JTokenType.Object or JTokenType.Array
                ? value.ToString(Formatting.None)
                : ScalarString(value);
        }

        private static string ScalarString(JToken v) =>
            v.Type switch
            {
                JTokenType.Null => string.Empty,
                JTokenType.String => v.Value<string>() ?? string.Empty,
                JTokenType.Boolean => v.Value<bool>() ? "true" : "false",
                JTokenType.Integer => v.Value<long>().ToString(CultureInfo.InvariantCulture),
                JTokenType.Float => v.Value<double>().ToString(CultureInfo.InvariantCulture),
                JTokenType.Object or JTokenType.Array => v.ToString(Formatting.None),
                _ => v.ToString()
            };
    }
}
