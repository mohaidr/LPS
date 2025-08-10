using Grpc.Net.Client.Balancer;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace LPS.Infrastructure.VariableServices.VariableHolders
{
    public class HttpResponseVariableHolder : IHttpResponseVariableHolder
    {
        private readonly IPlaceholderResolverService _placeholderResolverService;
        private readonly ILogger _logger;
        private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        private HttpResponseVariableHolder(IPlaceholderResolverService resolver, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _placeholderResolverService = resolver;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        // Required by IVariableHolder
        public VariableType? Type { get; private set; } = VariableType.HttpResponse;
        public bool IsGlobal { get; private set; }

        // Http-specific
        public IStringVariableHolder Body { get; private set; }
        public HttpStatusCode? StatusCode { get; private set; }
        public string StatusReason { get; private set; }

        // Case-insensitive headers store; supports multi-values per header
        private readonly Dictionary<string, List<string>> _headers =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, List<string>> Headers => _headers;


        IReadOnlyDictionary<string, IReadOnlyList<string>> IHttpResponseVariableHolder.Headers => throw new NotImplementedException();


        public ValueTask<string> GetRawValueAsync()
        {
            // For HttpResponse, the "raw value" is the response body
            return Body is null
                ? ValueTask.FromResult(string.Empty)
                : Body.GetRawValueAsync();
        }

        public async ValueTask<string> GetValueAsync(string? path, string sessionId, CancellationToken token)
        {
            var hash = this.GetHashCode();
            // No path => return raw body
            if (string.IsNullOrWhiteSpace(path))
                return await GetRawValueAsync();

            // Resolve placeholders in the path itself (if any)
            var resolvedPath = await _placeholderResolverService
                .ResolvePlaceholdersAsync<string>(path, sessionId, token);
            
            // .Body...
            if (resolvedPath.StartsWith(".Body", StringComparison.OrdinalIgnoreCase))
            {
                if (Body is null)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Body is not set on HttpResponseVariableHolder.", LPSLoggingLevel.Error, token);
                    throw new InvalidOperationException("Body is not set on HttpResponseVariableHolder.");
                }

                // As requested: pass the path, sessionId, and token to Body
                return await Body.GetValueAsync(resolvedPath, sessionId, token);
            }

            if (resolvedPath.Trim().Equals(".StatusReason", StringComparison.OrdinalIgnoreCase))
            {
                if (StatusReason != null)
                    return StatusReason;
                else
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "No status reason found", LPSLoggingLevel.Error, token);
                    throw new ArgumentException("No status reason found");
                };
            }

            if (resolvedPath.Trim().Equals(".StatusCode", StringComparison.OrdinalIgnoreCase))
            {
                if (!StatusCode.HasValue)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "No status code found", LPSLoggingLevel.Error, token);
                    throw new ArgumentException("No status code found");
                }
                return ((int)StatusCode).ToString();
            }

            // .Headers.HeaderName
            const string headersPrefix = ".Headers.";
            if (resolvedPath.StartsWith(headersPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return await ExtractHeader(resolvedPath, token);
            }

            await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Path '{resolvedPath}' is not supported. Use one of: '.Body...', '.StatusCode', '.StatusReason', or '.Headers.<name>'.", LPSLoggingLevel.Error, token);
            throw new NotSupportedException(
                $"Path '{resolvedPath}' is not supported. Use one of: '.Body...', '.StatusCode', '.StatusReason', or '.Headers.<name>'.");
        }

        public async ValueTask<string> ExtractHeader(string resolvedPath, CancellationToken token)
        {
            const string headersPrefix = ".Headers.";
            var keyWithIndex = resolvedPath.Substring(headersPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(keyWithIndex))
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Header name was not provided after '.Headers.'.", LPSLoggingLevel.Error, token);
                throw new ArgumentException("Header name was not provided after '.Headers.'.");
            }
            // Optional index syntax: HeaderName[2]
            int? index = null;
            var name = keyWithIndex;
            var lb = keyWithIndex.LastIndexOf('[');
            var rb = keyWithIndex.EndsWith("]") ? keyWithIndex.Length - 1 : -1;
            if (lb >= 0 && rb > lb)
            {
                var idxSpan = keyWithIndex.Substring(lb + 1, rb - lb - 1);
                if (int.TryParse(idxSpan, out var parsed))
                {
                    index = parsed;
                    name = keyWithIndex.Substring(0, lb);
                }
            }

            if (!_headers.TryGetValue(name, out var values) || values.Count == 0)
            {
                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Header '{name}' was not found.", LPSLoggingLevel.Error, token);
                throw new KeyNotFoundException($"Header '{name}' was not found.");
            }

            // Index requested
            if (index.HasValue)
            {
                var i = index.Value;
                if (i < 0 || i >= values.Count) throw new IndexOutOfRangeException($"Header '{name}' has {values.Count} value(s).");
                return values[i];
            }

            // No index: combine if safe; otherwise return first
            if (NonListValuedHeaders.Contains(name))
            {
                if (values.Count == 1) return values[0];

                await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, $"Multiple '{name}' headers found. Use an index or array access.", LPSLoggingLevel.Error, token);
                throw new InvalidOperationException($"Multiple '{name}' headers found. Use an index or array access.");
            }


            return string.Join(",", values); // OK for list-combinable headers
        }

        private static readonly HashSet<string> NonListValuedHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Set-Cookie",
            "SetCookie",
            "WWW-Authenticate",
            "WWWAuthenticate",
            "Proxy-Authenticate",
            "ProxyAuthenticate",
            "Authentication-Info",
            "AuthenticationInfo",
            "Proxy-Authentication-Info",
            "Proxy-AuthenticationInfo"
        };
        // -----------------------
        // Builder
        // -----------------------
        public class Builder
        {
            private readonly HttpResponseVariableHolder _holder;

            private readonly IPlaceholderResolverService _placeholderResolverService;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;



            public Builder(
                IPlaceholderResolverService placeholderResolverService,
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));
                _holder = new HttpResponseVariableHolder(_placeholderResolverService, _logger, _runtimeOperationIdProvider)
                {
                    Type = VariableType.HttpResponse
                };

            }


            public Builder SetGlobal(bool isGlobal = true)
            {
                _holder.IsGlobal = isGlobal;
                return this;
            }

            public Builder WithBody(IStringVariableHolder body)
            {
                _holder.Body = body ?? null;
                return this;
            }

            public Builder WithStatusCode(HttpStatusCode? statusCode)
            {
                _holder.StatusCode = statusCode ?? null;
                return this;
            }
            public Builder WithStatusReason(string reason)
            {
                _holder.StatusReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason;
                return this;
            }

            private static string NormalizeHeaderName(string key)
            {
                if (string.IsNullOrWhiteSpace(key)) return string.Empty;
                return key
                    .ToLowerInvariant()
                    .Replace("-", "")
                    .Replace("_", "")
                    .Trim();
            }


            public Builder WithHeader(string key, string value)
            {
                if (string.IsNullOrWhiteSpace(key)) return this;

                // add with the original name
                if (!_holder._headers.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    _holder._headers[key] = list;
                }
                list.Add(value);

                // add with a normalized header name
                var normalized = NormalizeHeaderName(key);
                if (!string.IsNullOrEmpty(normalized) &&
                    !normalized.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_holder._headers.TryGetValue(normalized, out var normList))
                    {
                        normList = new List<string>();
                        _holder._headers[normalized] = normList;
                    }
                    normList.Add(value);
                }

                return this;
            }

            public Builder WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
            {
                if (headers == null) return this;
                foreach (var kv in headers)
                    WithHeader(kv.Key, kv.Value);
                return this;
            }

            public Builder WithHeaders(IDictionary<string, IEnumerable<string>> headers)
            {
                if (headers == null) return this;
                foreach (var kv in headers)
                {
                    foreach (var v in kv.Value ?? Array.Empty<string>())
                        WithHeader(kv.Key, v);
                }
                return this;
            }




            public async ValueTask<HttpResponseVariableHolder> BuildAsync(CancellationToken token)
            {
                if (_holder.Body is null && _holder.StatusCode is null)
                {
                    await _logger.LogAsync(_runtimeOperationIdProvider.OperationId, "Body or StatusCode must be provided for HttpResponseVariableHolder", LPSLoggingLevel.Error, token);

                    throw new InvalidOperationException("Body must be provided for HttpResponseVariableHolder.");
                }

                return _holder;
            }
        }
    }
}
