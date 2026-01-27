#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LPS.Domain;
using LPS.Domain.Domain.Common.Enums;

namespace LPS.Infrastructure.Monitoring
{
    /// <summary>
    /// Service for evaluating failure rules and determining error status codes.
    /// Caches parsed filters per iteration to avoid repeated parsing.
    /// Thread-safe for concurrent access.
    /// </summary>
    public sealed class RuleService : IRuleService
    {
        /// <summary>
        /// Evaluates a comparison between a value and threshold(s).
        /// </summary>
        /// <param name="value">The actual value to compare</param>
        /// <param name="op">The comparison operator</param>
        /// <param name="threshold">The threshold value</param>
        /// <param name="thresholdMax">The second threshold (for 'between' operator)</param>
        /// <returns>True if the condition is met</returns>
        public static bool EvaluateCondition(double value, ComparisonOperator op, double threshold, double? thresholdMax = null)
        {
            return op switch
            {
                ComparisonOperator.GreaterThan => value > threshold,
                ComparisonOperator.LessThan => value < threshold,
                ComparisonOperator.GreaterThanOrEqual => value >= threshold,
                ComparisonOperator.LessThanOrEqual => value <= threshold,
                ComparisonOperator.Equals => Math.Abs(value - threshold) < 0.0001, // Float comparison tolerance
                ComparisonOperator.Between => thresholdMax.HasValue && value >= threshold && value <= thresholdMax.Value,
                ComparisonOperator.NotEquals => Math.Abs(value - threshold) >= 0.0001, // Float comparison tolerance
                _ => throw new ArgumentException($"Unsupported operator: {op}")
            };
        }

        // Cache key: (IterationId, RoundName) -> parsed filters
        private readonly ConcurrentDictionary<(Guid IterationId, string RoundName), IReadOnlyList<(ComparisonOperator Op, double Threshold, double? ThresholdMax)>> _cache = new();

        /// <summary>
        /// Gets the error status code filters for a given iteration.
        /// Results are cached per iteration ID and round name.
        /// </summary>
        public IReadOnlyList<(ComparisonOperator Op, double Threshold, double? ThresholdMax)> GetErrorStatusCodeFilters(
            HttpIteration iteration, 
            string roundName)
        {
            if (iteration == null) throw new ArgumentNullException(nameof(iteration));
            if (string.IsNullOrWhiteSpace(roundName)) throw new ArgumentException("Round name is required", nameof(roundName));

            var key = (iteration.Id, roundName);
            return _cache.GetOrAdd(key, _ => ExtractErrorStatusCodeFilters(iteration));
        }

        /// <summary>
        /// Checks if a given status code is considered an error based on the iteration's failure rules.
        /// </summary>
        public bool IsErrorStatusCode(HttpIteration iteration, string roundName, int statusCode)
        {
            var filters = GetErrorStatusCodeFilters(iteration, roundName);
            
            foreach (var (op, threshold, thresholdMax) in filters)
            {
                if (EvaluateCondition(statusCode, op, threshold, thresholdMax))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Extracts all errorStatusCodes from the iteration's failure rules that have ErrorRate metrics.
        /// If no ErrorRate rules are defined, defaults to >= 400.
        /// </summary>
        private static List<(ComparisonOperator Op, double Threshold, double? ThresholdMax)> ExtractErrorStatusCodeFilters(HttpIteration iteration)
        {
            var filters = new List<(ComparisonOperator Op, double Threshold, double? ThresholdMax)>();

            if (iteration.FailureRules != null && iteration.FailureRules.Count > 0)
            {
                foreach (var rule in iteration.FailureRules)
                {
                    // Check if this is an ErrorRate rule
                    if (MetricParser.TryParse(rule.Metric, out var parsed) && 
                        parsed.MetricName.Equals("ErrorRate", StringComparison.OrdinalIgnoreCase))
                    {
                        // Get the errorStatusCodes for this rule (default to >= 400 if not specified)
                        var statusCodeExpression = string.IsNullOrWhiteSpace(rule.ErrorStatusCodes) 
                            ? ">= 400" 
                            : rule.ErrorStatusCodes;

                        // Parse it as a StatusCode expression
                        if (MetricParser.TryParse($"StatusCode {statusCodeExpression}", out var statusParsed))
                        {
                            filters.Add((statusParsed.Operator, statusParsed.Value1, statusParsed.Value2));
                        }
                    }
                }
            }

            // If no ErrorRate rules found, use default >= 400
            if (filters.Count == 0)
            {
                filters.Add((ComparisonOperator.GreaterThanOrEqual, 400, null));
                filters.Add((ComparisonOperator.Equals, 0, null));
            }

            return filters;
        }

        /// <summary>
        /// Clears the cache for a specific iteration. 
        /// Should be called when an iteration completes to free memory.
        /// </summary>
        public void ClearCache(Guid iterationId, string roundName)
        {
            _cache.TryRemove((iterationId, roundName), out _);
        }

        /// <summary>
        /// Clears all cached filters. Use sparingly.
        /// </summary>
        public void ClearAllCache()
        {
            _cache.Clear();
        }
    }
}
