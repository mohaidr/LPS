using System;
using System.Text.RegularExpressions;

namespace LPS.Infrastructure.Monitoring
{
    /// <summary>
    /// Parses metric expressions from YAML rules into structured components.
    /// Supports expressions like: "ErrorRate > 0.05", "StatusCode >= 500", "TotalTime.P95 between 100 and 500"
    /// </summary>
    public static class MetricParser
    {
        private static readonly Regex _pattern = new Regex(
            @"^(?<metric>[\w\.]+)\s*(?<operator>>|<|>=|<=|!=|=|between)\s*(?<value1>-?[\d\.]+)(\s+and\s+(?<value2>-?[\d\.]+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Parses a metric expression string into its components.
        /// </summary>
        /// <param name="metricExpression">Expression like "ErrorRate > 0.05" or "TotalTime.P95 between 100 and 500"</param>
        /// <returns>Tuple containing (MetricName, Operator, Value1, Value2)</returns>
        /// <exception cref="ArgumentException">Thrown when expression is invalid</exception>
        public static (string MetricName, ComparisonOperator Operator, double Value1, double? Value2) Parse(string metricExpression)
        {
            if (string.IsNullOrWhiteSpace(metricExpression))
            {
                throw new ArgumentException("Metric expression cannot be null or empty", nameof(metricExpression));
            }

            var match = _pattern.Match(metricExpression.Trim());

            if (!match.Success)
            {
                throw new ArgumentException(
                    $"Invalid metric expression: '{metricExpression}'. " +
                    $"Expected format: 'MetricName <operator> value' (e.g., 'ErrorRate > 0.05')",
                    nameof(metricExpression)
                );
            }

            var metricName = match.Groups["metric"].Value;
            var operatorStr = match.Groups["operator"].Value.ToLower();
            var value1 = double.Parse(match.Groups["value1"].Value);
            var value2 = match.Groups["value2"].Success
                ? double.Parse(match.Groups["value2"].Value)
                : (double?)null;

            var op = operatorStr switch
            {
                ">" => ComparisonOperator.GreaterThan,
                "<" => ComparisonOperator.LessThan,
                ">=" => ComparisonOperator.GreaterThanOrEqual,
                "<=" => ComparisonOperator.LessThanOrEqual,
                "=" => ComparisonOperator.Equals,
                "between" => ComparisonOperator.Between,
                "!=" => ComparisonOperator.NotEquals,
                _ => throw new ArgumentException($"Unknown operator: '{operatorStr}'", nameof(metricExpression))
            };

            // Validate 'between' has two values
            if (op == ComparisonOperator.Between && !value2.HasValue)
            {
                throw new ArgumentException(
                    $"'between' operator requires two values (e.g., 'TotalTime.P95 between 100 and 500')",
                    nameof(metricExpression)
                );
            }

            return (metricName, op, value1, value2);
        }

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

        /// <summary>
        /// Tries to parse a metric expression without throwing exceptions.
        /// </summary>
        /// <param name="metricExpression">The expression to parse</param>
        /// <param name="result">The parsed result if successful</param>
        /// <returns>True if parsing was successful</returns>
        public static bool TryParse(string metricExpression, out (string MetricName, ComparisonOperator Operator, double Value1, double? Value2) result)
        {
            try
            {
                result = Parse(metricExpression);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
    }
}
