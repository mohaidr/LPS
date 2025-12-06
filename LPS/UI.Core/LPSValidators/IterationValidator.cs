using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using FluentValidation;
using LPS.UI.Common;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks.Dataflow;
using LPS.Domain.Domain.Common.Enums;
using LPS.UI.Common.DTOs;
using System.Net;
using LPS.Infrastructure.Monitoring;  // For MetricParser and ComparisonOperator

namespace LPS.UI.Core.LPSValidators
{
    internal partial class IterationValidator : CommandBaseValidator<HttpIterationDto>
    {
        readonly HttpIterationDto _iterationDto;

        // Supported metrics for validation (same as Domain validator)
        private static readonly HashSet<string> SupportedMetrics = new(StringComparer.OrdinalIgnoreCase)
        {
            // Scalar metrics
            "errorrate",
            // TotalTime
            "totaltime",
            // TTFB
            "ttfb", "timetofirstbyte",
            // WaitingTime
            "waitingtime", "waiting",
            // TCP
            "tcphandshake", "tcp",
            // TLS
            "tlshandshake", "tls", "ssl", "sslhandshake",
            // SendingTime
            "sendingtime", "sending", "upload", "upstream",
            // ReceivingTime
            "receivingtime", "receiving", "download", "downstream"
        };

        // Supported aggregations for timing metrics
        private static readonly HashSet<string> SupportedAggregations = new(StringComparer.OrdinalIgnoreCase)
        {
            "p50", "p90", "p95", "p99", "average", "avg", "min", "max", "sum"
        };

        // Scalar metrics that don't support aggregations
        private static readonly HashSet<string> ScalarMetrics = new(StringComparer.OrdinalIgnoreCase)
        {
            "errorrate"
        };

        public IterationValidator(HttpIterationDto iterationDto)
        {
            ArgumentNullException.ThrowIfNull(iterationDto);
            _iterationDto = iterationDto;


            RuleFor(dto => dto.Name)
                .NotNull()
                .WithMessage("The 'Name' must be a non-null value")
                .NotEmpty()
                .WithMessage("The 'Name' must not be empty")
                .Must(name =>
                {
                    // Check if the name matches the regex or starts with a placeholder
                    return (!string.IsNullOrEmpty(name) && name.StartsWith("$")) || NameRegex().IsMatch(name ?? string.Empty);
                })
                .WithMessage("The 'Name' must either start with '$' (for placeholders) or match the pattern: only alphanumeric characters, spaces, underscores, periods, and dashes are allowed")
                .Length(1, 60)
                .WithMessage("The 'Name' should be between 1 and 60 characters");


            RuleFor(dto => dto.StartupDelay)
                .Must(startupDelay =>
                {
                    return (int.TryParse(startupDelay, out int parsedValue) && parsedValue >= 0)
                    || string.IsNullOrEmpty(startupDelay)
                    || startupDelay.StartsWith("$");
                }).
                WithMessage("The 'StartupDelay' must be greater than or equal to 0 or a placeholder variable");

            RuleFor(dto => dto.Mode)
                .Must(mode =>
                {
                    // Check if the mode starts with `$` (indicating a placeholder)
                    if (!string.IsNullOrEmpty(mode) && mode.StartsWith("$"))
                        return true;

                    // Attempt to parse the mode as an IterationMode enum value
                    return Enum.TryParse<IterationMode>(mode, out _);
                })
                .WithMessage("The 'Mode' must be a valid IterationMode (e.g., DCB, CRB, CB, R, D) or start with '$'.");

            RuleFor(dto => dto.RequestCount)
                .Must(requestCount =>
                {
                    // Check if the value is a valid integer or a placeholder
                    return (int.TryParse(requestCount, out int parsedValue) && parsedValue > 0) || (!string.IsNullOrEmpty(requestCount) && requestCount.StartsWith("$"));
                })
                .WithMessage("The 'Request Count' must be a valid positive integer or a placeholder")
                .When(dto => dto.Mode == IterationMode.CRB.ToString() || dto.Mode == IterationMode.R.ToString()) // Mode is a string and must match IterationMode enum values
                .Must(requestCount =>
                {
                    // Ensure the value is null or a placeholder when the mode does not require RequestCount
                    return requestCount == null || requestCount.StartsWith("$");
                })
                .When(dto => dto.Mode != IterationMode.CRB.ToString() && dto.Mode != IterationMode.R.ToString(), ApplyConditionTo.CurrentValidator)
                .Must((dto, requestCount) =>
                {
                    // Compare RequestCount and BatchSize when both are valid integers
                    if ((!string.IsNullOrEmpty(requestCount) && requestCount.StartsWith("$")) || (!string.IsNullOrEmpty(dto.BatchSize) && dto.BatchSize.StartsWith("$")))
                        return true;

                    if (int.TryParse(requestCount, out int parsedRequestCount) &&
                        int.TryParse(dto.BatchSize, out int parsedBatchSize))
                    {
                        return parsedRequestCount > parsedBatchSize;
                    }

                    return false; // Validation fails if parsing fails
                })
                .WithMessage("The 'Request Count' must be greater than the 'Batch Size'")
                .When(dto => dto.Mode == "CRB" && !string.IsNullOrWhiteSpace(dto.BatchSize), ApplyConditionTo.CurrentValidator);

            RuleFor(dto => dto.Duration)
                .Must(duration =>
                {
                    // Check if the value is a valid integer or a placeholder
                    return (int.TryParse(duration, out int parsedValue) && parsedValue > 0) || (!string.IsNullOrEmpty(duration) && duration.StartsWith("$"));
                })
                .WithMessage("The 'Duration' must be a valid positive integer or a placeholder")
                .When(dto => dto.Mode == IterationMode.D.ToString() || dto.Mode == IterationMode.DCB.ToString())
                .Must(duration =>
                {
                    // Ensure the value is null or a placeholder when the mode does not require Duration
                    return duration == null || duration.StartsWith("$");
                })
                .When(dto => dto.Mode != IterationMode.D.ToString() && dto.Mode != IterationMode.DCB.ToString(), ApplyConditionTo.CurrentValidator)
                .Must((dto, duration) =>
                {
                    // Compare Duration and CoolDownTime when both are valid integers
                    if ((!string.IsNullOrEmpty(duration) && duration.StartsWith("$")) || (!string.IsNullOrEmpty(dto.CoolDownTime) && dto.CoolDownTime.StartsWith("$")))
                        return true;

                    if (int.TryParse(duration, out int parsedDuration) &&
                        int.TryParse(dto.CoolDownTime, out int parsedCoolDownTime))
                    {
                        return parsedDuration * 1000 > parsedCoolDownTime;
                    }

                    return false; // Validation fails if parsing fails
                })
                .WithMessage("The 'Duration * 1000' must be greater than the 'Cool Down Time'")
                .When(dto => dto.Mode == IterationMode.DCB.ToString() && !string.IsNullOrWhiteSpace(dto.CoolDownTime), ApplyConditionTo.CurrentValidator);

            RuleFor(dto => dto.BatchSize)
                .Must(batchSize =>
                {
                    // Check if the value is a valid integer or a placeholder
                    return (int.TryParse(batchSize, out int parsedValue) && parsedValue > 0) || (!string.IsNullOrEmpty(batchSize) && batchSize.StartsWith("$"));
                })
                .WithMessage("The 'Batch Size' must be a valid positive integer or a placeholder")
                .When(dto => dto.Mode == IterationMode.DCB.ToString() || dto.Mode == IterationMode.CRB.ToString() || dto.Mode == IterationMode.CB.ToString())
                .Must(batchSize =>
                {
                    // Ensure the value is null or a placeholder when the mode does not require BatchSize
                    return batchSize == null || batchSize.StartsWith("$");
                })
                .When(dto => dto.Mode != IterationMode.DCB.ToString() && dto.Mode != IterationMode.CRB.ToString() && dto.Mode != IterationMode.CB.ToString(), ApplyConditionTo.CurrentValidator)
                .Must((dto, batchSize) =>
                {
                    // Compare BatchSize and RequestCount when both are valid integers
                    if ((!string.IsNullOrEmpty(batchSize) && batchSize.StartsWith("$")) || (!string.IsNullOrEmpty(dto.RequestCount) && dto.RequestCount.StartsWith("$")))
                        return true;

                    if (int.TryParse(batchSize, out int parsedBatchSize) &&
                        int.TryParse(dto.RequestCount, out int parsedRequestCount))
                    {
                        return parsedBatchSize < parsedRequestCount;
                    }

                    return false; // Validation fails if parsing fails
                })
                .WithMessage("The 'Batch Size' must be less than the 'Request Count'")
                .When(dto => dto.Mode == IterationMode.CRB.ToString() && !string.IsNullOrWhiteSpace(dto.RequestCount), ApplyConditionTo.CurrentValidator);

            RuleFor(dto => dto.CoolDownTime)
                .Must(coolDownTime =>
                {
                    // Check if the value is a valid integer or a placeholder
                    return (int.TryParse(coolDownTime, out int parsedValue) && parsedValue > 0) || (!string.IsNullOrEmpty(coolDownTime) && coolDownTime.StartsWith("$"));
                })
                .WithMessage("The 'Cool Down Time' must be a valid positive integer or a placeholder")
                .When(dto => dto.Mode == IterationMode.DCB.ToString() || dto.Mode == IterationMode.CRB.ToString() || dto.Mode == IterationMode.CB.ToString())
                .Must(coolDownTime =>
                {
                    // Ensure the value is null or a placeholder when the mode does not require CoolDownTime
                    return coolDownTime == null || coolDownTime.StartsWith("$");
                })
                .When(dto => dto.Mode != IterationMode.DCB.ToString() && dto.Mode != IterationMode.CRB.ToString() && dto.Mode != IterationMode.CB.ToString(), ApplyConditionTo.CurrentValidator)
                .Must((dto, coolDownTime) =>
                {
                    // Compare CoolDownTime and Duration when both are valid integers
                    if ((!string.IsNullOrEmpty(coolDownTime) && coolDownTime.StartsWith("$")) || (!string.IsNullOrEmpty(dto.Duration) && dto.Duration.StartsWith("$")))
                        return true;

                    if (int.TryParse(coolDownTime, out int parsedCoolDownTime) &&
                        int.TryParse(dto.Duration, out int parsedDuration))
                    {
                        return parsedCoolDownTime < parsedDuration * 1000;
                    }

                    return false; // Validation fails if parsing fails
                })
                .WithMessage("The 'CoolDownTime' must be less than the 'Duration * 1000'")
                .When(dto => dto.Mode == IterationMode.DCB.ToString() && !string.IsNullOrWhiteSpace(dto.Duration), ApplyConditionTo.CurrentValidator);

            RuleFor(dto => dto.MaximizeThroughput)
             .Must(maximizeThroughput =>
             {
                 return bool.TryParse(maximizeThroughput, out _) || string.IsNullOrEmpty(maximizeThroughput) || maximizeThroughput.StartsWith("$");
             })
             .WithMessage("The 'MaximizeThroughput' must be a valid boolean");
            
            RuleFor(dto => dto.TerminationRules)
                .Must(BeValidTerminationRules)
                .WithMessage("Termination rules must have valid metric expressions (e.g., 'ErrorRate > 0.1', 'TotalTime.P95 > 5000') and positive grace periods.");
            
            RuleFor(dto => dto.FailureRules)
                .Must(BeValidFailureRules)
                .WithMessage("Failure rules must have valid metric expressions (e.g., 'ErrorRate > 0.1', 'TotalTime.P95 > 5000'). Use 'errorStatusCodes' to filter by status codes.");

            RuleFor(dto => dto.HttpRequest)
                .SetValidator(new RequestValidator(new HttpRequestDto()));

        }

        private bool BeValidTerminationRules(IEnumerable<TerminationRuleDto> rules)
        {
            if (rules == null) return true;
            if (!rules.Any()) return true;

            return rules.All(rule =>
            {
                // Validate GracePeriod
                if (string.IsNullOrWhiteSpace(rule.GracePeriod))
                    return false;

                bool validGrace = rule.GracePeriod.StartsWith("$") ||
                                  (TimeSpan.TryParse(rule.GracePeriod, out var gracePeriod) && gracePeriod > TimeSpan.Zero);

                if (!validGrace)
                    return false;

                // Validate Metric expression
                if (string.IsNullOrWhiteSpace(rule.Metric))
                    return false;

                // If it's a placeholder, accept it
                if (rule.Metric.StartsWith("$"))
                    return true;

                // Validate metric expression format and content
                if (!IsValidMetricExpression(rule.Metric))
                    return false;

                // Validate ErrorStatusCodes if provided (for ErrorRate metrics)
                if (!string.IsNullOrWhiteSpace(rule.ErrorStatusCodes))
                {
                    if (!rule.ErrorStatusCodes.StartsWith("$") && !IsValidStatusCodeExpression(rule.ErrorStatusCodes))
                        return false;
                }

                return true;
            });
        }

        private bool BeValidFailureRules(IEnumerable<FailureRuleDto> rules)
        {
            if (rules == null) return true;
            if (!rules.Any()) return true;

            return rules.All(rule =>
            {
                if (string.IsNullOrWhiteSpace(rule.Metric))
                    return false;

                if (rule.Metric.StartsWith("$"))
                    return true;

                // Validate metric expression format and content
                if (!IsValidMetricExpression(rule.Metric))
                    return false;

                // Validate ErrorStatusCodes if provided (for ErrorRate metrics)
                if (!string.IsNullOrWhiteSpace(rule.ErrorStatusCodes))
                {
                    if (!rule.ErrorStatusCodes.StartsWith("$") && !IsValidStatusCodeExpression(rule.ErrorStatusCodes))
                        return false;
                }

                return true;
            });
        }

        /// <summary>
        /// Validates a metric expression format and content.
        /// Supported formats:
        /// - "ErrorRate > 0.1"
        /// - "TotalTime.P95 > 5000"
        /// - "TTFB.Average < 1000"
        /// - "TotalTime.P50 between 100 and 500"
        /// </summary>
        private static bool IsValidMetricExpression(string metricExpression)
        {
            if (string.IsNullOrWhiteSpace(metricExpression))
                return false;

            // First try to parse with MetricParser to validate syntax
            if (!MetricParser.TryParse(metricExpression, out var parsed))
                return false;

            var (fullMetricName, op, threshold, thresholdMax) = parsed;

            // For 'between' operator, ensure both values are present
            if (op == ComparisonOperator.Between && !thresholdMax.HasValue)
                return false;

            // Parse metric name and aggregation
            var parts = fullMetricName.Split('.');
            var metricName = parts[0].ToLower();
            var aggregation = parts.Length > 1 ? parts[1].ToLower() : null;

            // Validate metric name is supported
            if (!SupportedMetrics.Contains(metricName))
                return false;

            // Validate aggregation rules
            bool isScalarMetric = ScalarMetrics.Contains(metricName);
            
            if (aggregation != null)
            {
                // Scalar metrics (ErrorRate) don't support aggregations
                if (isScalarMetric)
                    return false;
                    
                // For timing metrics, aggregation must be valid
                if (!SupportedAggregations.Contains(aggregation))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates an error status codes expression.
        /// This is the operator + value portion (not including "StatusCode").
        /// Examples: ">= 500", "between 400 and 499", "= 429"
        /// </summary>
        private static bool IsValidStatusCodeExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            // Pattern: operator value [and value2]
            // Examples: ">= 500", "between 400 and 499", "= 429", ">= 0" (connection failures)
            var pattern = @"^(?<operator>>|<|>=|<=|!=|=|between)\s*(?<value1>\d{1,3})(\s+and\s+(?<value2>\d{1,3}))?$";
            var match = Regex.Match(expression.Trim(), pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
                return false;

            var operatorStr = match.Groups["operator"].Value.ToLower();
            var value2Group = match.Groups["value2"];

            // Validate 'between' operator has two values
            if (operatorStr == "between" && !value2Group.Success)
                return false;

            // Validate status codes are in valid range: 0 (connectivity failure) or 100-599 (HTTP codes)
            // Note: 0 represents connectivity failures (DNS, connection refused, timeout, etc.)
            // Codes 1-99 are not valid HTTP status codes
            if (!int.TryParse(match.Groups["value1"].Value, out var code1) || (code1 != 0 && (code1 < 100 || code1 > 599)))
                return false;

            if (value2Group.Success && (!int.TryParse(value2Group.Value, out var code2) || (code2 != 0 && (code2 < 100 || code2 > 599))))
                return false;

            return true;
        }



        public override HttpIterationDto Dto { get { return _iterationDto; } }

        [GeneratedRegex("^[a-zA-Z0-9 _.-]+$")]
        private static partial Regex NameRegex();
    }
}
