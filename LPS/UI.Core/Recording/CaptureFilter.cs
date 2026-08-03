#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LPS.UI.Core.Recording.Har;

namespace LPS.UI.Core.Recording
{
    /// <summary>
    /// Decides which captured HAR entries survive into the generated plan. Users can ignore entries
    /// by response content-type, URL file extension, HTTP method, or Playwright resource type. Sensible
    /// defaults drop common static-asset noise; user-provided values are added on top of the defaults.
    /// </summary>
    internal sealed class CaptureFilter
    {
        private static readonly string[] DefaultIgnoreResourceTypes =
            { "image", "stylesheet", "font", "media", "manifest", "other" };

        private static readonly string[] DefaultIgnoreExtensions =
            { ".css", ".js", ".mjs", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp",
              ".ico", ".woff", ".woff2", ".ttf", ".eot", ".map", ".mp4", ".avif" };

        private static readonly string[] DefaultIgnoreContentTypes =
            { "text/css", "javascript", "image/", "font/", "video/", "audio/" };

        // Always-noise infrastructure paths (Cloudflare challenge/RUM, etc.) — never real app traffic.
        private static readonly string[] DefaultIgnorePaths =
            { "/cdn-cgi/" };

        // Browser-fingerprint headers dropped by --minimal-headers (matched as name prefixes).
        private static readonly string[] MinimalHeaderPrefixes =
            { "sec-ch-", "sec-fetch-", "priority", "upgrade-insecure-requests" };

        private readonly HashSet<string> _ignoreResourceTypes;
        private readonly HashSet<string> _ignoreExtensions;
        private readonly List<string> _ignoreContentTypes;
        private readonly HashSet<string> _ignoreMethods;
        private readonly HashSet<string> _onlyHosts;
        private readonly HashSet<string> _ignoreHosts;
        private readonly List<string> _ignorePaths;
        private readonly List<string> _ignoreHeaderPatterns;
        private readonly bool _minimalHeaders;

        public CaptureFilter(
            IEnumerable<string>? ignoreContentTypes = null,
            IEnumerable<string>? ignoreExtensions = null,
            IEnumerable<string>? ignoreMethods = null,
            IEnumerable<string>? ignoreResourceTypes = null,
            IEnumerable<string>? onlyHosts = null,
            IEnumerable<string>? ignoreHosts = null,
            IEnumerable<string>? ignorePaths = null,
            IEnumerable<string>? ignoreHeaders = null,
            bool minimalHeaders = false)
        {
            _ignoreResourceTypes = ToLowerSet(DefaultIgnoreResourceTypes.Concat(ignoreResourceTypes ?? Enumerable.Empty<string>()));
            _ignoreMethods = ToLowerSet(ignoreMethods ?? Enumerable.Empty<string>());
            _ignoreContentTypes = ToLowerSet(DefaultIgnoreContentTypes.Concat(ignoreContentTypes ?? Enumerable.Empty<string>())).ToList();
            _ignoreExtensions = ToLowerSet(
                DefaultIgnoreExtensions.Concat((ignoreExtensions ?? Enumerable.Empty<string>()).Select(NormalizeExtension)));
            _onlyHosts = NormalizeHosts(onlyHosts);
            _ignoreHosts = NormalizeHosts(ignoreHosts);
            _ignorePaths = ToLowerSet(DefaultIgnorePaths.Concat(ignorePaths ?? Enumerable.Empty<string>())).ToList();
            _ignoreHeaderPatterns = (ignoreHeaders ?? Enumerable.Empty<string>())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h.Trim().ToLowerInvariant())
                .ToList();
            _minimalHeaders = minimalHeaders;
        }

        public bool ShouldKeep(HarEntry entry)
        {
            if (entry?.Request?.Url is null || string.IsNullOrWhiteSpace(entry.Request.Method))
                return false;

            var host = string.Empty;
            var pathAndQuery = entry.Request.Url;
            if (Uri.TryCreate(entry.Request.Url, UriKind.Absolute, out var uri))
            {
                host = uri.Host.ToLowerInvariant();
                pathAndQuery = uri.AbsolutePath + uri.Query;
            }
            pathAndQuery = pathAndQuery.ToLowerInvariant();

            // Host allowlist: when set, keep ONLY these hosts (kills third-party analytics/CDN domains).
            if (_onlyHosts.Count > 0 && !_onlyHosts.Contains(host))
                return false;

            if (_ignoreHosts.Contains(host))
                return false;

            // Path denylist: drop by URL path/query substring (e.g. /cdn-cgi/, /collect).
            if (_ignorePaths.Any(p => pathAndQuery.Contains(p)))
                return false;

            var method = entry.Request.Method.Trim().ToLowerInvariant();
            if (_ignoreMethods.Contains(method))
                return false;

            var resourceType = entry.ResourceType?.Trim().ToLowerInvariant() ?? string.Empty;
            if (resourceType.Length > 0 && _ignoreResourceTypes.Contains(resourceType))
                return false;

            if (HasIgnoredExtension(entry.Request.Url))
                return false;

            var contentType = entry.Response?.Content?.MimeType?.Trim().ToLowerInvariant() ?? string.Empty;
            if (contentType.Length > 0 && _ignoreContentTypes.Any(ct => contentType.Contains(ct)))
                return false;

            return true;
        }

        // Drops a header matching --minimal-headers noise or an --ignore-header pattern:
        // exact name (case-insensitive), or a name prefix when the pattern ends with '*'.
        public bool ShouldDropHeader(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;

            var lower = name.ToLowerInvariant();
            if (_minimalHeaders && MinimalHeaderPrefixes.Any(p => lower.StartsWith(p, StringComparison.Ordinal)))
                return true;

            foreach (var pattern in _ignoreHeaderPatterns)
            {
                var isMatch = pattern.EndsWith("*", StringComparison.Ordinal)
                    ? lower.StartsWith(pattern[..^1], StringComparison.Ordinal)
                    : string.Equals(lower, pattern, StringComparison.Ordinal);
                if (isMatch)
                    return true;
            }

            return false;
        }

        private bool HasIgnoredExtension(string url)
        {
            if (_ignoreExtensions.Count == 0)
                return false;

            var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : url;

            var lastSlash = path.LastIndexOf('/');
            var segment = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;

            var dot = segment.LastIndexOf('.');
            if (dot < 0)
                return false;

            var extension = segment.Substring(dot).ToLowerInvariant();
            return _ignoreExtensions.Contains(extension);
        }

        private static string NormalizeExtension(string extension)
        {
            var value = (extension ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length == 0)
                return value;
            return value.StartsWith('.') ? value : "." + value;
        }

        private static HashSet<string> NormalizeHosts(IEnumerable<string>? hosts)
        {
            return (hosts ?? Enumerable.Empty<string>())
                .Select(NormalizeHost)
                .Where(h => h.Length > 0)
                .ToHashSet();
        }

        // Accepts a bare host, host:port, or a full URL and returns just the lowercase host, so both
        // "--only-host fakestoreapi.com" and "--only-host https://fakestoreapi.com/" work.
        private static string NormalizeHost(string value)
        {
            var v = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Length == 0)
                return v;

            if (Uri.TryCreate(v, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                return uri.Host.Trim('.');

            var scheme = v.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
                v = v.Substring(scheme + 3);

            var cut = v.IndexOfAny(new[] { '/', '?', '#' });
            if (cut >= 0)
                v = v.Substring(0, cut);

            var colon = v.IndexOf(':');
            if (colon >= 0)
                v = v.Substring(0, colon);

            return v.Trim('.');
        }

        private static HashSet<string> ToLowerSet(IEnumerable<string> values)
        {
            return values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant())
                .ToHashSet();
        }
    }
}
