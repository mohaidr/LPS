using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using LPS.Domain.Common;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.LPSClients.SessionManager;
using LPS.Infrastructure.VariableServices.GlobalVariableManager;

namespace LPS.Infrastructure.PlaceHolderService.Methods
{
    /// <summary>
    /// $findxml(source=$xml.Body, path=/orders/order, where=status="shipped", select=id, match=first, variable=x)
    /// The XPath-based counterpart of $find for XML sources. Selects a node set with `path` (XPath),
    /// optionally filters it with `where` (an XPath predicate — no brackets), projects a value with
    /// `select` (a relative XPath), and stores the result using the same match modes as $find
    /// (first | last | index:N | count | all).
    ///
    /// Namespaces:
    ///   - Declare prefixes with `namespaces=o=urn:acme:orders;s=http://schemas.xmlsoap.org/soap/envelope/`
    ///     and use them in path/where/select (e.g. path=/s:Envelope/s:Body).
    ///   - Or set `ignoreNamespaces=true` to strip all namespaces from the source so plain, unprefixed
    ///     XPath (e.g. /orders/order) matches regardless of any xmlns on the document.
    /// </summary>
    public sealed class FindXmlMethod : MethodBase
    {
        public FindXmlMethod(ParameterExtractorService p, ILogger l, IRuntimeOperationIdProvider op, IVariableManager v, Lazy<IPlaceholderResolverService> r, ISessionManager sessionManager)
            : base(p, l, op, v, r, sessionManager)
        {
        }

        public override string Name => "findxml";

        private enum MatchMode { First, Last, Index, Count, All }

        public override async Task<string> ExecuteAsync(string parameters, string sessionId, CancellationToken token)
        {
            string variableName = string.Empty;
            bool isGlobal = false;

            try
            {
                var args = _params.ParseParameters(parameters);

                // 'source' is resolved here: methods now receive their args RAW (the outer resolver no
                // longer pre-resolves method arguments). 'default' stays raw (resolved only on the no-match
                // fallback). Everything else is resolved so it can be indirected through a variable
                // (e.g. path=${myPath}).
                var source = await ResolveArgAsync(args, "source", string.Empty, sessionId, token);
                var defaultRaw = args.GetValueOrDefault("default", string.Empty);

                var path = await ResolveArgAsync(args, "path", "/*/*", sessionId, token);
                var where = await ResolveArgAsync(args, "where", string.Empty, sessionId, token);
                var select = await ResolveArgAsync(args, "select", string.Empty, sessionId, token);
                var match = (await ResolveArgAsync(args, "match", "first", sessionId, token)).Trim();
                var asType = (await ResolveArgAsync(args, "as", "auto", sessionId, token)).Trim();
                var namespaces = await ResolveArgAsync(args, "namespaces", string.Empty, sessionId, token);
                variableName = await ResolveArgAsync(args, "variable", string.Empty, sessionId, token);
                isGlobal = await _params.ExtractBoolAsync(parameters, "isGlobal", false, sessionId, token);
                var ignoreNamespaces = await _params.ExtractBoolAsync(parameters, "ignoreNamespaces", false, sessionId, token);

                var (mode, indexTarget) = ParseMatchMode(match);

                if (string.IsNullOrWhiteSpace(source))
                {
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token, isGlobal);
                }

                // XmlResolver = null disables external entity resolution (protects against XXE).
                var doc = new XmlDocument { XmlResolver = null };
                try
                {
                    // ignoreNamespaces strips all namespace info up front so unprefixed XPath matches
                    // regardless of any xmlns on the document; otherwise the source is loaded as-is.
                    doc.LoadXml(ignoreNamespaces ? StripNamespaces(source) : source);
                }
                catch
                {
                    await _logger.LogAsync(_op.OperationId, "findxml failed. The source could not be parsed as XML.", LPSLoggingLevel.Warning, token);
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token, isGlobal);
                }

                // Always non-null (empty when nothing is declared) so a single SelectNodes/SelectSingleNode
                // overload is used everywhere. Lets path/where/select reference declared prefixes; when the
                // document was stripped there is simply nothing left to bind.
                var nsmgr = BuildNamespaceManager(doc, ignoreNamespaces ? string.Empty : namespaces);

                var effectivePath = string.IsNullOrWhiteSpace(path) ? "/*/*" : path.Trim();
                if (!string.IsNullOrWhiteSpace(where))
                {
                    effectivePath = $"{effectivePath}[{where.Trim()}]";
                }

                XmlNodeList nodes;
                try
                {
                    nodes = doc.SelectNodes(effectivePath, nsmgr);
                }
                catch (Exception ex)
                {
                    await _logger.LogAsync(_op.OperationId, $"findxml failed. Invalid XPath '{effectivePath}': {ex.Message}", LPSLoggingLevel.Warning, token);
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token, isGlobal);
                }

                if (mode == MatchMode.Count)
                {
                    return await StoreAndReturnAsync(variableName, new JValue(nodes?.Count ?? 0), asType, token, isGlobal, sessionId);
                }

                var values = new List<string>();
                if (nodes != null)
                {
                    foreach (XmlNode node in nodes)
                    {
                        token.ThrowIfCancellationRequested();
                        values.Add(ProjectXml(node, select, nsmgr));

                        if (mode == MatchMode.First) break;
                        if (mode == MatchMode.Index && values.Count - 1 == indexTarget) break;
                    }
                }

                if (mode == MatchMode.All)
                {
                    return await StoreAndReturnAsync(variableName, new JArray(values.Select(v => (JToken)new JValue(v))), asType, token, isGlobal, sessionId);
                }

                string picked = mode switch
                {
                    MatchMode.Last => values.Count > 0 ? values[^1] : null,
                    MatchMode.Index => indexTarget < values.Count ? values[indexTarget] : null,
                    _ => values.Count > 0 ? values[0] : null
                };

                if (picked == null)
                {
                    return await StoreNoMatchAsync(variableName, defaultRaw, asType, mode, sessionId, token, isGlobal);
                }

                return await StoreAndReturnAsync(variableName, new JValue(picked), asType, token, isGlobal, sessionId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(_op.OperationId, $"findxml failed. {ex}", LPSLoggingLevel.Error, token);
                await StoreTypedVariableAsync(variableName, BuildValueToken(string.Empty, "string"), "string", token, isGlobal, sessionId);
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
        private static string ProjectXml(XmlNode node, string select, XmlNamespaceManager nsmgr)
        {
            if (string.IsNullOrWhiteSpace(select))
            {
                return node.Value ?? node.InnerText ?? string.Empty;
            }

            var selected = node.SelectSingleNode(select.Trim(), nsmgr);
            if (selected == null) return string.Empty;
            return selected.Value ?? selected.InnerText ?? string.Empty;
        }

        /// <summary>
        /// Builds an <see cref="XmlNamespaceManager"/> from a <c>prefix=uri;prefix=uri</c> declaration so
        /// path/where/select can reference namespaced nodes by prefix. Always returns a (possibly empty)
        /// manager; unparseable bindings are skipped. Split on the FIRST '=' only because namespace URIs
        /// routinely contain ':' and '/'.
        /// </summary>
        private static XmlNamespaceManager BuildNamespaceManager(XmlDocument doc, string namespaces)
        {
            var mgr = new XmlNamespaceManager(doc.NameTable);
            if (string.IsNullOrWhiteSpace(namespaces))
                return mgr;

            foreach (var binding in namespaces.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(binding)) continue;

                var eq = binding.IndexOf('=');
                if (eq <= 0) continue;

                var prefix = binding.Substring(0, eq).Trim();
                var uri = binding.Substring(eq + 1).Trim();
                if (prefix.Length == 0 || uri.Length == 0) continue;

                mgr.AddNamespace(prefix, uri);
            }

            return mgr;
        }

        /// <summary>
        /// Returns an equivalent XML string with every namespace removed: element and attribute names are
        /// reduced to their local name and all xmlns declarations are dropped. Lets plain, unprefixed XPath
        /// match documents that use default or prefixed namespaces. Uses <see cref="XElement"/> whose parser
        /// prohibits DTD processing by default, so it stays XXE-safe.
        /// </summary>
        private static string StripNamespaces(string xml)
        {
            return StripElement(XElement.Parse(xml, LoadOptions.None)).ToString(SaveOptions.DisableFormatting);
        }

        private static XElement StripElement(XElement element)
        {
            var stripped = new XElement(element.Name.LocalName);

            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration) continue;
                stripped.SetAttributeValue(attribute.Name.LocalName, attribute.Value);
            }

            foreach (var node in element.Nodes())
            {
                if (node is XElement child)
                    stripped.Add(StripElement(child));
                else
                    stripped.Add(node);
            }

            return stripped;
        }

        private async Task<string> StoreNoMatchAsync(string variableName, string defaultRaw, string asType, MatchMode mode, string sessionId, CancellationToken token, bool isGlobal)
        {
            if (mode == MatchMode.Count)
                return await StoreAndReturnAsync(variableName, new JValue(0), asType, token, isGlobal, sessionId);

            if (mode == MatchMode.All)
                return await StoreAndReturnAsync(variableName, new JArray(), asType, token, isGlobal, sessionId);

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

            return await StoreAndReturnAsync(variableName, defaultToken, asType, token, isGlobal, sessionId);
        }

        private async Task<string> StoreAndReturnAsync(string variableName, JToken value, string asType, CancellationToken token, bool isGlobal, string sessionId)
        {
            await StoreTypedVariableAsync(variableName, value, asType, token, isGlobal, sessionId);
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
