using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;

namespace LPS.Domain
{
    public partial class HttpIteration
    {
        public new class Validator : CommandBaseValidator<HttpIteration, HttpIteration.SetupCommand>
        {
            public override SetupCommand Command => _command;
            public override HttpIteration Entity => _entity;
            private readonly ILogger _logger;
            private readonly IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            private readonly SetupCommand _command;
            private readonly HttpIteration _entity;
            ISkipIfEvaluator _skipIfEvaluator;

            // Supported metrics for validation
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

            public Validator(HttpIteration entity, SetupCommand command, 
                ISkipIfEvaluator skipIfEvaluator,
                ILogger logger, 
                IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _command = command;
                _entity = entity;
                _skipIfEvaluator = skipIfEvaluator;

                #region Validation Rules
                RuleFor(c => c.Name)
                    .Must(BeValidName)
                    .WithMessage("The 'Name' must be non-null, non-empty, 1-60 characters, and contain only letters, numbers, spaces, or _.-");

                RuleFor(c => c.StartupDelay)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("The 'StartupDelay' must be greater than or equal to 0");

                RuleFor(c => c.Mode)
                    .NotNull()
                    .WithMessage("The 'Mode' must be one of: DCB, CRB, CB, R, D");

                RuleFor(c => c.MaximizeThroughput)
                    .NotNull()
                    .WithMessage("The 'MaximizeThroughput' property must be non-null");

                RuleFor(c => c.RequestCount)
                .Must(BeValidRequestCount)
                .WithMessage(c => $"The 'RequestCount' {(c.Mode == IterationMode.CRB || c.Mode == IterationMode.R ? $"must be specified and greater than 0{(c.Mode == IterationMode.CRB && c.BatchSize.HasValue ? ", it must greater than BatchSize as well" : ".")}" : "must be not be provided")} for Mode '{c.Mode}'.");

                RuleFor(c => c.Duration)
                    .Must(BeValidDuration)
                    .WithMessage(c => $"The 'Duration' {(c.Mode == IterationMode.D || c.Mode == IterationMode.DCB ? $"must be specified and greater than 0{(c.Mode == IterationMode.DCB && c.CoolDownTime.HasValue ? ", it must greater than CoolDownTime/1000 as well" : ".")}" : "must be not be provided")} for Mode '{c.Mode}'.");

                RuleFor(c => c.BatchSize)
                    .Must(BeValidBatchSize)
                    .WithMessage(c => $"The 'BatchSize' {(c.Mode == IterationMode.DCB || c.Mode == IterationMode.CRB || c.Mode == IterationMode.CB ? $"must be specified and greater than 0{(c.Mode == IterationMode.CRB && c.RequestCount.HasValue ? ", it must be less than RequestCount as well" : ".")}" : "must be not be provided")} for Mode '{c.Mode}'.");

                RuleFor(c => c.CoolDownTime)
                    .Must(BeValidCoolDownTime)
                    .WithMessage(c => $"The 'CoolDownTime' {(c.Mode == IterationMode.DCB || c.Mode == IterationMode.CRB || c.Mode == IterationMode.CB ? $"must be specified and greater than 0{(c.Mode == IterationMode.DCB && c.Duration.HasValue ? ", it must be less than Duration*1000 as well" : ".")}" : "must be not be provided")} for Mode '{c.Mode}'.");

                RuleFor(c => c.TerminationRules)
                    .Must(BeValidTerminationRules)
                    .WithMessage("Termination rules must have valid metric expressions (e.g., 'ErrorRate > 0.1', 'TotalTime.P95 > 5000') and positive grace periods.");

                RuleFor(c => c.FailureRules)
                    .Must(BeValidFailureRules)
                    .WithMessage("Failure rules must have valid metric expressions (e.g., 'ErrorRate > 0.1', 'TotalTime.P95 > 5000'). Use 'errorStatusCodes' to filter by status codes.");

                #endregion

                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Http Run: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }
                command.IsValid = base.Validate();
            }

            #region Validation Methods
            private bool BeValidName(string name)
            {
                return name != null &&
                       !string.IsNullOrEmpty(name) &&
                       name.Length >= 1 && name.Length <= 60 &&
                       Regex.IsMatch(name, @"^[a-zA-Z0-9 _.-]+$");
            }

            private bool BeValidRequestCount(SetupCommand command, int? requestCount)
            {
                if (command.Mode == IterationMode.CRB || command.Mode == IterationMode.R)
                {
                    return requestCount.HasValue &&
                           requestCount > 0 &&
                           (command.Mode != IterationMode.CRB || !command.BatchSize.HasValue || requestCount > command.BatchSize);
                }
                return !requestCount.HasValue;
            }

            private bool BeValidDuration(SetupCommand command, int? duration)
            {
                if (command.Mode == IterationMode.D || command.Mode == IterationMode.DCB)
                {
                    return duration.HasValue &&
                           duration > 0 &&
                           (command.Mode != IterationMode.DCB || !command.CoolDownTime.HasValue || duration * 1000 > command.CoolDownTime);
                }
                return !duration.HasValue;
            }

            private bool BeValidBatchSize(SetupCommand command, int? batchSize)
            {
                if (command.Mode == IterationMode.DCB || command.Mode == IterationMode.CRB || command.Mode == IterationMode.CB)
                {
                    return batchSize.HasValue &&
                           batchSize > 0 &&
                           (command.Mode != IterationMode.CRB || !command.RequestCount.HasValue || batchSize < command.RequestCount);
                }
                return !batchSize.HasValue;
            }

            private bool BeValidCoolDownTime(SetupCommand command, int? coolDownTime)
            {
                if (command.Mode == IterationMode.DCB || command.Mode == IterationMode.CRB || command.Mode == IterationMode.CB)
                {
                    return coolDownTime.HasValue &&
                           coolDownTime > 0 &&
                           (command.Mode != IterationMode.DCB || !command.Duration.HasValue || coolDownTime < command.Duration * 1000);
                }
                return !coolDownTime.HasValue;
            }

            private bool BeValidTerminationRules(IEnumerable<TerminationRule> rules)
            {
                if (rules == null) return true;
                if (!rules.Any()) return true;

                return rules.All(rule =>
                    rule.GracePeriod > TimeSpan.Zero &&
                    !string.IsNullOrWhiteSpace(rule.Metric) &&
                    IsValidMetricExpression(rule.Metric) &&
                    (string.IsNullOrWhiteSpace(rule.ErrorStatusCodes) || IsValidStatusCodeExpression(rule.ErrorStatusCodes)));
            }

            private bool BeValidFailureRules(IEnumerable<FailureRule> rules)
            {
                if (rules == null) return true;
                if (!rules.Any()) return true;

                return rules.All(rule => 
                    !string.IsNullOrWhiteSpace(rule.Metric) &&
                    IsValidMetricExpression(rule.Metric) &&
                    (string.IsNullOrWhiteSpace(rule.ErrorStatusCodes) || IsValidStatusCodeExpression(rule.ErrorStatusCodes)));
            }

            /// <summary>
            /// Validates a metric expression format.
            /// Supported formats:
            /// - "ErrorRate > 0.1"
            /// - "TotalTime.P95 > 5000"
            /// - "TTFB.Average < 1000"
            /// - "TotalTime.P50 between 100 and 500"
            /// </summary>
            private bool IsValidMetricExpression(string metricExpression)
            {
                if (string.IsNullOrWhiteSpace(metricExpression))
                    return false;

                // Pattern: MetricName[.Aggregation] <operator> value [and value2]
                var pattern = @"^(?<metric>[\w]+)(\.(?<aggregation>[\w]+))?\s*(?<operator>>|<|>=|<=|!=|=|between)\s*(?<value1>-?[\d\.]+)(\s+and\s+(?<value2>-?[\d\.]+))?$";
                var match = Regex.Match(metricExpression.Trim(), pattern, RegexOptions.IgnoreCase);

                if (!match.Success)
                    return false;

                var metricName = match.Groups["metric"].Value.ToLower();
                var aggregation = match.Groups["aggregation"].Success ? match.Groups["aggregation"].Value.ToLower() : null;
                var operatorStr = match.Groups["operator"].Value.ToLower();
                var value2Group = match.Groups["value2"];

                // Validate metric name
                if (!SupportedMetrics.Contains(metricName))
                    return false;

                // Validate aggregation for timing metrics (not required for ErrorRate)
                bool isScalarMetric = metricName == "errorrate";
                if (aggregation != null)
                {
                    if (isScalarMetric)
                        return false; // Scalar metrics don't support aggregations
                    if (!SupportedAggregations.Contains(aggregation))
                        return false;
                }

                // Validate 'between' operator has two values
                if (operatorStr == "between" && !value2Group.Success)
                    return false;

                // Validate value1 is a valid number
                if (!double.TryParse(match.Groups["value1"].Value, out _))
                    return false;

                // Validate value2 if present
                if (value2Group.Success && !double.TryParse(value2Group.Value, out _))
                    return false;

                return true;
            }

            /// <summary>
            /// Validates an error status codes expression.
            /// This is the operator + value portion (not including "StatusCode").
            /// Examples: ">= 500", "between 400 and 499", "= 429"
            /// </summary>
            private bool IsValidStatusCodeExpression(string expression)
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

                if (value2Group.Success && (!int.TryParse(value2Group.Value, out var code2) || code2 < 0 || code2 > 599))
                    return false;

                return true;
            }
            #endregion
        }
    }
}