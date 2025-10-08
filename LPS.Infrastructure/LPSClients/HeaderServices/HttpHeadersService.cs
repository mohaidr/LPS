using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain;
using LPS.Domain.Common.Interfaces;
using LPS.Infrastructure.Common.Interfaces;
using LPS.Domain.LPSRequest.LPSHttpRequest; // <- use URL service

namespace LPS.Infrastructure.LPSClients.HeaderServices
{
    public enum HeaderValidationMode { Strict, Lenient, RawPassthrough }

    public class HttpHeadersService : IHttpHeadersService
    {
        private readonly IPlaceholderResolverService _placeHolderResolver;
        private readonly HeaderValidationMode _mode;
        private readonly bool _allowHostOverride;
        private readonly IClientConfiguration<HttpRequest> _config;

        private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Content-Length", "Transfer-Encoding", "Connection", "TE", "Upgrade"
        };

        private static readonly HashSet<string> LenientAllowList = new(StringComparer.OrdinalIgnoreCase)
        {
            "User-Agent","Referer","Referrer","Cookie","Origin","Pragma","Warning",
            "Traceparent","Tracestate","X-Request-Id","X-Correlation-Id",
            "X-B3-TraceId","X-B3-SpanId","X-B3-ParentSpanId","X-B3-Sampled","X-B3-Flags",
            "X-*","x-amz-*","x-ms-*","x-goog-*","cf-*","fastly-*","akamai-*"
        };

        public HttpHeadersService(IClientConfiguration<HttpRequest> config, IPlaceholderResolverService placeHolderResolver)
        {
            _config = config;
            _placeHolderResolver = placeHolderResolver;
            var lpsCfg = (ILPSHttpClientConfiguration<HttpRequest>)_config;
            _mode = lpsCfg.HeaderMode;
            _allowHostOverride = lpsCfg.AllowHostOverride;
        }

        public async Task ApplyHeadersAsync(HttpRequestMessage httpRequestMessage, string sessionId, Dictionary<string, string> httpHeaders, CancellationToken token)
        {
            if (httpRequestMessage == null || httpHeaders == null || httpHeaders.Count == 0) return;

            var method = httpRequestMessage.Method?.Method ?? "GET";
            bool supportContentHeaders =
                   method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                   method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                   method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);

            foreach (var kv in httpHeaders)
            {
                var name = kv.Key?.Trim();
                var rawValue = kv.Value;

                if (!IsSafeHeaderPair(name, rawValue))
                    throw new NotSupportedException($"Unsafe or empty header: '{name}'.");

                var resolvedValue = await _placeHolderResolver.ResolvePlaceholdersAsync<string>(rawValue, sessionId, token);

                // --- Host override via URL service ---
                if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    if (_mode == HeaderValidationMode.Strict)
                        throw new NotSupportedException("Host override is disabled in Strict mode.");

                    if (_mode == HeaderValidationMode.Lenient && !_allowHostOverride)
                        throw new NotSupportedException("Host override is disabled by policy (AllowHostOverride=false).");

                    // Use your URL parser to validate/extract host[:port].
                    // It accepts: host, host:port, http(s)://host[:port]/path?query, placeholders, etc.
                    var parsed = new URL(resolvedValue);
                    if (string.IsNullOrEmpty(parsed.HostName))
                        throw new NotSupportedException("Invalid Host value. Expected 'host' or 'host:port' (URL with host also allowed).");

                    httpRequestMessage.Headers.Host = parsed.HostName; // sets Host / :authority
                    continue;
                }
                // -------------------------------------

                if (ForbiddenHeaders.Contains(name) && _mode != HeaderValidationMode.RawPassthrough)
                    throw new NotSupportedException($"Header '{name}' is managed by the client/tool.");

                if (TrySetTypedHeader(httpRequestMessage, name, resolvedValue, supportContentHeaders))
                    continue;

                if (_mode == HeaderValidationMode.Strict)
                    throw new NotSupportedException($"Unsupported or invalid header: {name}");

                if (_mode == HeaderValidationMode.Lenient && !IsLenientAllowed(name))
                    throw new NotSupportedException($"Header '{name}' is not allowed in Lenient mode unless it parses.");

                if (!httpRequestMessage.Headers.TryAddWithoutValidation(name, resolvedValue))
                {
                    if (httpRequestMessage.Content == null)
                        httpRequestMessage.Content = new ByteArrayContent(Array.Empty<byte>());

                    if (!httpRequestMessage.Content.Headers.TryAddWithoutValidation(name, resolvedValue))
                        throw new NotSupportedException($"Could not add header: {name}");
                }
            }
        }

        private static bool IsLenientAllowed(string name)
        {
            if (LenientAllowList.Contains(name)) return true;
            if (name.StartsWith("X-", StringComparison.OrdinalIgnoreCase)) return true;

            var lowered = name.ToLowerInvariant();
            return lowered.StartsWith("x-amz-") || lowered.StartsWith("x-ms-") || lowered.StartsWith("x-goog-")
                || lowered.StartsWith("cf-") || lowered.StartsWith("fastly-") || lowered.StartsWith("akamai-");
        }

        private static bool IsSafeHeaderPair(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) return false;
            if (name.IndexOfAny(new[] { '\r', '\n' }) >= 0) return false;
            if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0) return false;
            if (name.Length > 256) return false;
            if (value.Length > 16_384) return false;
            return true;
        }

        /// <summary>
        /// Tries to set a single HTTP header on the request using typed
        /// System.Net.Http.Headers models (e.g., Authorization, Content-Type).
        /// Returns true if the value parses and is applied; otherwise false.
        /// Content headers are handled when supportContentHeaders is true.
        /// </summary>
        private static bool TrySetTypedHeader(HttpRequestMessage message, string name, string value, bool supportContentHeaders)
        {
            if (name.Equals("Referrer", StringComparison.OrdinalIgnoreCase)) name = "Referer";

            if (supportContentHeaders && IsContentHeader(name))
            {
                if (message.Content == null)
                    message.Content = new ByteArrayContent(Array.Empty<byte>());

                switch (name.ToLowerInvariant())
                {
                    case "content-type":
                        if (!(message.Content is MultipartContent) && MediaTypeHeaderValue.TryParse(value.Trim(), out var ctype)) { message.Content.Headers.ContentType = ctype; return true; }
                        return false;
                    case "content-encoding":
                        foreach (var enc in value.Split(',')) { var e = enc.Trim(); if (!string.IsNullOrEmpty(e)) message.Content.Headers.ContentEncoding.Add(e); }
                        return true;
                    case "content-language":
                        foreach (var lang in value.Split(',')) { var l = lang.Trim(); if (!string.IsNullOrEmpty(l)) message.Content.Headers.ContentLanguage.Add(l); }
                        return true;
                    case "content-disposition":
                        if (ContentDispositionHeaderValue.TryParse(value, out var cd)) { message.Content.Headers.ContentDisposition = cd; return true; }
                        return false;
                    case "content-md5":
                        try { message.Content.Headers.ContentMD5 = Convert.FromBase64String(value.Trim()); return true; } catch { return false; }
                    case "content-length":
                        return false; // computed by HttpClient
                    default:
                        return false;
                }
            }

            switch (name.ToLowerInvariant())
            {
                case "authorization":
                    if (AuthenticationHeaderValue.TryParse(value.Trim(), out var authValue)) { message.Headers.Authorization = authValue; return true; }
                    return false;

                case "accept":
                    return TrySplitParse(value, v => MediaTypeWithQualityHeaderValue.TryParse(v, out var t) ? (true, t) : (false, null), t => message.Headers.Accept.Add(t));
                case "accept-charset":
                    return TrySplitParse(value, v => StringWithQualityHeaderValue.TryParse(v, out var ch) ? (true, ch) : (false, null), ch => message.Headers.AcceptCharset.Add(ch));
                case "accept-encoding":
                    return TrySplitParse(value, v => StringWithQualityHeaderValue.TryParse(v, out var enc) ? (true, enc) : (false, null), enc => message.Headers.AcceptEncoding.Add(enc));
                case "accept-language":
                    return TrySplitParse(value, v => StringWithQualityHeaderValue.TryParse(v, out var lang) ? (true, lang) : (false, null), lang => message.Headers.AcceptLanguage.Add(lang));

                case "user-agent":
                    if (message.Headers.UserAgent.TryParseAdd(value)) return true;
                    return false;

                case "connection":
                    foreach (var c in value.Split(',')) { var token = c.Trim(); if (string.IsNullOrEmpty(token)) continue; message.Headers.Connection.Add(token); if (token.Equals("close", StringComparison.OrdinalIgnoreCase)) message.Headers.ConnectionClose = true; }
                    return true;

                case "host": // handled earlier
                    return false;

                case "transfer-encoding":
                    var ok = TrySplitParse(value, v => TransferCodingHeaderValue.TryParse(v, out var te) ? (true, te) : (false, null),
                                           te => message.Headers.TransferEncoding.Add(te));
                    if (ok && value.Split(',').Any(v => v.Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase)))
                        message.Headers.TransferEncodingChunked = true;
                    return ok;

                case "upgrade":
                    if (ProductHeaderValue.TryParse(value.Trim(), out var up)) { message.Headers.Upgrade.Add(up); return true; }
                    return false;

                case "pragma":
                    message.Headers.Pragma.Add(new NameValueHeaderValue(value.Trim())); return true;

                case "cache-control":
                    if (CacheControlHeaderValue.TryParse(value, out var cc)) { message.Headers.CacheControl = cc; return true; }
                    return false;

                case "expect":
                    message.Headers.ExpectContinue = value.Trim().Equals("100-continue", StringComparison.OrdinalIgnoreCase); return true;

                case "date":
                    if (DateTimeOffset.TryParse(value, out var date)) { message.Headers.Date = date; return true; }
                    return false;

                case "from":
                    message.Headers.From = value.Trim(); return true;

                case "if-match":
                    return TrySplitParse(value, v => EntityTagHeaderValue.TryParse(v, out var etag) ? (true, etag) : (false, null), etag => message.Headers.IfMatch.Add(etag));
                case "if-none-match":
                    return TrySplitParse(value, v => EntityTagHeaderValue.TryParse(v, out var etag2) ? (true, etag2) : (false, null), etag2 => message.Headers.IfNoneMatch.Add(etag2));

                case "if-unmodified-since":
                    if (DateTimeOffset.TryParse(value, out var ius)) { message.Headers.IfUnmodifiedSince = ius; return true; }
                    return false;
                case "if-modified-since":
                    if (DateTimeOffset.TryParse(value, out var ims)) { message.Headers.IfModifiedSince = ims; return true; }
                    return false;

                case "max-forwards":
                    if (int.TryParse(value, out var mf)) { message.Headers.MaxForwards = mf; return true; }
                    return false;

                case "proxy-authorization":
                    if (AuthenticationHeaderValue.TryParse(value, out var pa)) { message.Headers.ProxyAuthorization = pa; return true; }
                    return false;

                case "range":
                    if (RangeHeaderValue.TryParse(value, out var range)) { message.Headers.Range = range; return true; }
                    return false;

                case "if-range":
                    if (RangeConditionHeaderValue.TryParse(value, out var ifr)) { message.Headers.IfRange = ifr; return true; }
                    return false;

                case "referer":
                    if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var refUri)) { message.Headers.Referrer = refUri; return true; }
                    return false;

                case "te":
                    return TrySplitParse(value, v => TransferCodingWithQualityHeaderValue.TryParse(v, out var teq) ? (true, teq) : (false, null), teq => message.Headers.TE.Add(teq));

                case "trailer":
                    foreach (var t in value.Split(',')) { var tv = t.Trim(); if (!string.IsNullOrEmpty(tv)) message.Headers.Trailer.Add(tv); }
                    return true;

                case "via":
                    return TrySplitParse(value, v => ViaHeaderValue.TryParse(v, out var via) ? (true, via) : (false, null), via => message.Headers.Via.Add(via));

                default:
                    return false;
            }
        }

        private static bool IsContentHeader(string name) =>
            name.Equals("content-type", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-encoding", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-language", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-length", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-disposition", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("content-md5", StringComparison.OrdinalIgnoreCase);

        private static bool TrySplitParse<T>(string value, Func<string, (bool ok, T parsed)> tryParse, Action<T> add)
        {
            bool overall = true;
            foreach (var part in value.Split(','))
            {
                var v = part.Trim();
                if (string.IsNullOrEmpty(v)) continue;
                var (ok, parsed) = tryParse(v);
                if (!ok) { overall = false; continue; }
                add(parsed);
            }
            return overall;
        }
    }
}
