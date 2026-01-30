#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using LPS.Infrastructure.Common.Interfaces;

namespace LPS.Infrastructure.LPSClients.ServerTiming
{
    /// <summary>
    /// Represents the breakdown of server processing time from Server-Timing header.
    /// </summary>
    public struct ServerTimeBreakdown
    {
        /// <summary>Overall server processing time.</summary>
        public double Total;
        /// <summary>Database query time (db, database, mysql, postgres, sql, etc.).</summary>
        public double DB;
        /// <summary>Cache lookup time (cache, redis, memcached, etc.).</summary>
        public double Cache;
        /// <summary>Application processing time (app, compute, process, etc.).</summary>
        public double App;
    }

    /// <summary>
    /// Parses server processing time from HTTP response headers.
    /// Supports W3C Server-Timing header format and custom headers with numeric values.
    /// </summary>
    public static class ServerTimingParser
    {
        // Known metric name aliases for categorization
        private static readonly HashSet<string> DbAliases = new(StringComparer.OrdinalIgnoreCase)
            { "db", "database", "mysql", "postgres", "sql", "sqlserver", "mongodb", "mongo", "redis-db", "query" };
        
        private static readonly HashSet<string> CacheAliases = new(StringComparer.OrdinalIgnoreCase)
            { "cache", "redis", "memcached", "memcache", "varnish", "cdn", "hit", "miss" };
        
        private static readonly HashSet<string> AppAliases = new(StringComparer.OrdinalIgnoreCase)
            { "app", "application", "compute", "process", "processing", "handler", "controller", "logic" };
        
        private static readonly HashSet<string> TotalAliases = new(StringComparer.OrdinalIgnoreCase)
            { "total", "server", "response", "duration", "time", "elapsed" };

        // Precompiled regex patterns for better performance
        private static readonly Regex DurRegex = new(@"(\w+)(?:;[^;,]*)*?;dur=(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SimpleDurRegex = new(@"(\w+);dur=(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DurOnlyRegex = new(@"dur=(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Attempts to extract server processing time from response headers.
        /// </summary>
        /// <param name="responseHeaders">The response headers to search.</param>
        /// <param name="headerName">The header name to look for (e.g., "Server-Timing", "X-Response-Time").</param>
        /// <param name="format">The expected format of the header value.</param>
        /// <param name="breakdown">The extracted server time breakdown with individual components.</param>
        /// <returns>True if server time was successfully extracted; otherwise, false.</returns>
        public static bool TryParse(
            HttpResponseHeaders responseHeaders,
            string? headerName,
            ServerTimeFormat format,
            out ServerTimeBreakdown breakdown)
        {
            breakdown = new ServerTimeBreakdown();

            if (string.IsNullOrWhiteSpace(headerName))
            {
                return false;
            }

            if (!responseHeaders.TryGetValues(headerName, out var headerValues))
            {
                return false;
            }

            // Concatenate multiple header values (Server-Timing allows multiple headers)
            var headerValue = string.Join(", ", headerValues);
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                return false;
            }

            return format switch
            {
                ServerTimeFormat.ServerTiming => TryParseServerTimingHeader(headerValue, out breakdown),
                ServerTimeFormat.Milliseconds => TryParseNumericValue(headerValue, 1.0, out breakdown),
                ServerTimeFormat.Seconds => TryParseNumericValue(headerValue, 1000.0, out breakdown),
                ServerTimeFormat.Auto => TryParseAuto(headerValue, out breakdown),
                _ => false
            };
        }

        /// <summary>
        /// Auto-detect format: try Server-Timing first, then numeric with optional 'ms' or 's' suffix.
        /// </summary>
        internal static bool TryParseAuto(string headerValue, out ServerTimeBreakdown breakdown)
        {
            breakdown = new ServerTimeBreakdown();

            // Try Server-Timing format first (contains 'dur=')
            if (headerValue.Contains("dur=", StringComparison.OrdinalIgnoreCase))
            {
                return TryParseServerTimingHeader(headerValue, out breakdown);
            }

            // Try numeric with optional suffix (assume milliseconds as default)
            return TryParseNumericValue(headerValue, 1.0, out breakdown);
        }

        /// <summary>
        /// Parse W3C Server-Timing header format.
        /// Examples: "total;dur=123.4", "db;dur=53, app;dur=47.2", "cache;desc=HIT"
        /// Extracts breakdown metrics: total, db, cache, app.
        /// </summary>
        internal static bool TryParseServerTimingHeader(string headerValue, out ServerTimeBreakdown breakdown)
        {
            breakdown = new ServerTimeBreakdown();

            var matches = DurRegex.Matches(headerValue);

            if (matches.Count == 0)
            {
                // Try simpler pattern: metric;dur=value with metric at start
                matches = SimpleDurRegex.Matches(headerValue);

                if (matches.Count == 0)
                {
                    // Last resort: just dur=value anywhere (sum all)
                    var durMatches = DurOnlyRegex.Matches(headerValue);

                    if (durMatches.Count == 0)
                    {
                        return false;
                    }

                    // Sum all dur values into total
                    foreach (Match match in durMatches)
                    {
                        if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var durValue))
                        {
                            breakdown.Total += durValue;
                        }
                    }
                    return breakdown.Total > 0;
                }
            }

            double summedTotal = 0;
            bool hasExplicitTotal = false;

            foreach (Match match in matches)
            {
                var metricName = match.Groups[1].Value;
                if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var durValue))
                {
                    continue;
                }

                summedTotal += durValue;

                // Categorize based on metric name
                if (TotalAliases.Contains(metricName))
                {
                    breakdown.Total = durValue;
                    hasExplicitTotal = true;
                }
                else if (DbAliases.Contains(metricName))
                {
                    breakdown.DB += durValue;
                }
                else if (CacheAliases.Contains(metricName))
                {
                    breakdown.Cache += durValue;
                }
                else if (AppAliases.Contains(metricName))
                {
                    breakdown.App += durValue;
                }
            }

            // If no explicit 'total' metric, use sum of all metrics
            if (!hasExplicitTotal)
            {
                breakdown.Total = summedTotal;
            }

            return breakdown.Total > 0;
        }

        /// <summary>
        /// Parse a simple numeric value with a multiplier (for seconds to ms conversion).
        /// </summary>
        internal static bool TryParseNumericValue(string headerValue, double multiplier, out ServerTimeBreakdown breakdown)
        {
            breakdown = new ServerTimeBreakdown();
            var trimmed = headerValue.Trim();

            // Remove common suffixes for parsing
            if (trimmed.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^2].Trim();
                multiplier = 1.0; // Already in ms
            }
            else if (trimmed.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^1].Trim();
                multiplier = 1000.0; // Convert to ms
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                breakdown.Total = value * multiplier;
                return true;
            }
            return false;
        }
    }
}
