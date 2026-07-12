using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $findxml(source=$xml.Body, path=/orders/order, where=status="shipped", select=id, match=first, variable=x)
    /// The XPath-based counterpart of $find for XML sources. Selects a node set with `path` (XPath),
    /// optionally filters it with `where` (an XPath predicate — no brackets), projects a value with
    /// `select` (a relative XPath), and stores the result using the same match modes as $find
    /// (first | last | index:N | count | all).
    /// </summary>
    public sealed class FindXmlMethod : MethodBase
    {
        public FindXmlMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r)
            : base(p, l, op, v, r)
        {
        }

        public override string Name => "findxml";

        private enum MatchMode { First, Last, Index, Count, All }

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;

            try
            {
                var args = _params.ParseParameters(parameters);

                // 'source' and 'default' are taken RAW (the outer resolver pre-resolves method arguments
                // containing '$' before dispatch). Everything else is resolved so it can be indirected
                // through a variable (e.g. path=${myPath}).
                var source = args.GetValueOrDefault("source", string.Empty);
                var defaultRaw = args.GetValueOrDefault("default", string.Empty);

                var path = await ResolveArgAsync(args, "path", "/*/*", sessionId, token);
                var where = await ResolveArgAsync(args, "where", string.Empty, sessionId, token);
                var select = await ResolveArgAsync(args, "select", string.Empty, sessionId, token);
                var match = (await ResolveArgAsync(args, "match", "first", sessionId, token)).Trim();
                var asType = (await ResolveArgAsync(args, "as", "auto", sessionId, token)).Trim();
                variableName = await ResolveArgAsync(args, "variable", string.Empty, sessionId, token);

                var (mode, indexTarget) = ParseMatchMode(match);

                if (string.IsNullOrWhiteSpace(source))
                {
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token);
                }

                // XmlResolver = null disables external entity resolution (protects against XXE).
                var doc = new XmlDocument { XmlResolver = null };
                try
                {
                    doc.LoadXml(source);
                }
                catch
                {
                    await _logger.LogAsync(_op.OperationId, "findxml failed. The source could not be parsed as XML.", LPSLoggingLevel.Warning, token);
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token);
                }

                var effectivePath = string.IsNullOrWhiteSpace(path) ? "/*/*" : path.Trim();
                if (!string.IsNullOrWhiteSpace(where))
                {
                    effectivePath = $"{effectivePath}[{where.Trim()}]";
                }

                XmlNodeList nodes;
                try
                {
                    nodes = doc.SelectNodes(effectivePath);
                }
                catch (Exception ex)
                {
                    await _logger.LogAsync(_op.OperationId, $"findxml failed. Invalid XPath '{effectivePath}': {ex.Message}", LPSLoggingLevel.Warning, token);
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token);
                }

                if (mode == MatchMode.Count)
                {
                    return await StoreAndReturnAsync(variableName, new JValue(nodes?.Count ?? 0), asType, token);
                }

                var values = new List<string>();
                if (nodes != null)
                {
                    foreach (XmlNode node in nodes)
                    {
                        token.ThrowIfCancellationRequested();
                        values.Add(ProjectXml(node, select));

                        if (mode == MatchMode.First) break;
                        if (mode == MatchMode.Index && values.Count - 1 == indexTarget) break;
                    }
                }

                if (mode == MatchMode.All)
                {
                    return await StoreAndReturnAsync(variableName, new JArray(values.Select(v => (JToken)new JValue(v))), asType, token);
                }

                string picked = mode switch
                {
                    MatchMode.Last => values.Count > 0 ? values[^1] : null,
                    MatchMode.Index => indexTarget < values.Count ? values[indexTarget] : null,
                    _ => values.Count > 0 ? values[0] : null
                };

                if (picked == null)
                {
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token);
                }

                return await StoreAndReturnAsync(variableName, new JValue(picked), asType, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"findxml failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreVariableIfNeededAsync(variableName, string.Empty, token);
                return string.Empty;
            }
        }

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

        /// <summary>
        /// Projects a value from a matched node. With no <paramref name="select"/> the node's own text
        /// (or attribute value) is used; otherwise <paramref name="select"/> is a relative XPath
        /// (e.g. <c>id</c>, <c>@status</c>, <c>text()</c>).
        /// </summary>
        private static string ProjectXml(XmlNode node, string select)
        {
            if (string.IsNullOrWhiteSpace(select))
            {
                return node.Value ?? node.InnerText ?? string.Empty;
            }

            var selected = node.SelectSingleNode(select.Trim());
            if (selected == null) return string.Empty;
            return selected.Value ?? selected.InnerText ?? string.Empty;
        }

        private async Task<string> StoreNoMatchAsync(string variableName, string defaultRaw, string asType, MatchMode mode, string sessionId, CancellationToken token)
        {
            if (mode == MatchMode.Count)
                return await StoreAndReturnAsync(variableName, new JValue(0), asType, token);

            if (mode == MatchMode.All)
                return await StoreAndReturnAsync(variableName, new JArray(), asType, token);

            JToken defaultToken;
            if (string.IsNullOrWhiteSpace(defaultRaw))
            {
                defaultToken = new JValue(string.Empty);
            }
            else
            {
                var resolved = await _resolver.Value.ResolvePlaceholdersAsync<string>(defaultRaw, sessionId, token);
                defaultToken = new JValue(resolved ?? string.Empty);
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

        private static string DisplayString(JToken value)
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
