using Grpc.Net.Client.Balancer;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Infrastructure.Common.Interfaces;
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
        private readonly VBuilder _builder;
        public IVariableBuilder Builder => _builder;

        private HttpResponseVariableHolder(IPlaceholderResolverService resolver,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider, 
            VBuilder builder)
        {
            _placeholderResolverService = resolver;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _builder = builder;
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
        public sealed class VBuilder : IVariableBuilder
        {
            private readonly IPlaceholderResolverService _placeholderResolverService;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;

            // Pre-created holder (do not touch until BuildAsync)
            private readonly HttpResponseVariableHolder _holder;

            // Local fields to store data before assigning to the holder
            private bool _isGlobal;
            private IStringVariableHolder _body;
            private HttpStatusCode? _statusCode;
            private string _statusReason = string.Empty;
            private readonly Dictionary<string, List<string>> _headers = new(StringComparer.OrdinalIgnoreCase);

            public VBuilder(
                IPlaceholderResolverService placeholderResolverService,
                ILogger logger,
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _placeholderResolverService = placeholderResolverService ?? throw new ArgumentNullException(nameof(placeholderResolverService));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _runtimeOperationIdProvider = runtimeOperationIdProvider ?? throw new ArgumentNullException(nameof(runtimeOperationIdProvider));

                // Create the holder once and keep it untouched until BuildAsync
                _holder = new HttpResponseVariableHolder(_placeholderResolverService, _logger, _runtimeOperationIdProvider, this)
                {
                    Type = VariableType.HttpResponse
                };
            }

            public VBuilder SetGlobal(bool isGlobal = true)
            {
                _isGlobal = isGlobal; // buffer locally
                return this;
            }

            public VBuilder WithBody(IStringVariableHolder body)
            {
                _body = body; // buffer locally
                return this;
            }

            public VBuilder WithStatusCode(HttpStatusCode? statusCode)
            {
                _statusCode = statusCode; // buffer locally
                return this;
            }

            public VBuilder WithStatusReason(string reason)
            {
                _statusReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason; // buffer locally
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

            public VBuilder WithHeader(string key, string value)
            {
                if (string.IsNullOrWhiteSpace(key)) return this;

                // Buffer with the original name
                if (!_headers.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    _headers[key] = list;
                }
                list.Add(value);

                // Buffer with a normalized header name as well
                var normalized = NormalizeHeaderName(key);
                if (!string.IsNullOrEmpty(normalized) &&
                    !normalized.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_headers.TryGetValue(normalized, out var normList))
                    {
                        normList = new List<string>();
                        _headers[normalized] = normList;
                    }
                    normList.Add(value);
                }

                return this;
            }

            public VBuilder WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
            {
                if (headers == null) return this;
                foreach (var kv in headers)
                    WithHeader(kv.Key, kv.Value);
                return this;
            }

            public VBuilder WithHeaders(IDictionary<string, IEnumerable<string>> headers)
            {
                if (headers == null) return this;
                foreach (var kv in headers)
                {
                    foreach (var v in kv.Value ?? Array.Empty<string>())
                        WithHeader(kv.Key, v);
                }
                return this;
            }

            public async ValueTask<IVariableHolder> BuildAsync(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();

                // Validate local buffered state before assigning to the holder
                if (_body is null && _statusCode is null)
                {
                    await _logger.LogAsync(
                        _runtimeOperationIdProvider.OperationId,
                        "Body or StatusCode must be provided for HttpResponseVariableHolder",
                        LPSLoggingLevel.Error,
                        token);

                    throw new InvalidOperationException("Body or StatusCode must be provided for HttpResponseVariableHolder.");
                }

                // Assign all buffered values to the pre-created holder
                _holder.IsGlobal = _isGlobal;
                _holder.Body = _body;
                _holder.StatusCode = _statusCode;
                _holder.StatusReason = _statusReason;

                // Copy buffered headers into the holder
                foreach (var kv in _headers)
                {
                    if (!_holder._headers.TryGetValue(kv.Key, out var list))
                    {
                        list = new List<string>();
                        _holder._headers[kv.Key] = list;
                    }
                    list.AddRange(kv.Value);
                }

                return _holder; // return the same instance created in the constructor
            }
        }

    }
}
