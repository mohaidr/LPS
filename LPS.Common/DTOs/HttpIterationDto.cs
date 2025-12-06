using LPS.Domain;
using System;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using System.Collections.Generic;

namespace LPS.UI.Common.DTOs
{
    public class HttpIterationDto : DtoBase<HttpIterationDto>
    {
        public HttpIterationDto()
        {
            Name = string.Empty;
            HttpRequest = new HttpRequestDto();
            TerminationRules = [];
            FailureRules = [];
        }

        // Name of the iteration
        public string Name { get; set; }

        // HTTP request details
        public HttpRequestDto HttpRequest { get; set; }

        // Startup delay (can be a variable)
        public string StartupDelay { get; set; }

        // Maximize throughput (can be a variable)
        public string MaximizeThroughput { get; set; }

        // Iteration mode (can be a variable)
        private string _mode;
        public string Mode
        {
            get => string.IsNullOrWhiteSpace(_mode) ? "R" : _mode;
            set => _mode = value;
        }

        // Request count (can be a variable)
        private string _requestCount;
        public string RequestCount
        {
            get => ((string.IsNullOrWhiteSpace(_mode) || (_mode?.Equals("R", StringComparison.OrdinalIgnoreCase) ?? false)) && string.IsNullOrWhiteSpace(_requestCount)) ? "1" : _requestCount;
            set => _requestCount = value;
        }

        // Duration (can be a variable)
        public string Duration { get; set; }

        // Batch size (can be a variable)
        public string BatchSize { get; set; }

        // Cooldown time (can be a variable)
        public string CoolDownTime { get; set; }

        // Evaluator condition (can be a variable)
        public string SkipIf { get; set; }

        // Inline operator support for failure rules
        public List<FailureRuleDto> FailureRules { get; set; }
        
        // Inline operator support for termination rules
        public List<TerminationRuleDto> TerminationRules { get; set; }

        // Deep copy method to create a new instance with the same data
        public void DeepCopy(out HttpIterationDto targetDto)
        {
            targetDto = new HttpIterationDto
            {
                Name = this.Name,
                StartupDelay = this.StartupDelay,
                MaximizeThroughput = this.MaximizeThroughput,
                Mode = this.Mode,
                RequestCount = this.RequestCount,
                Duration = this.Duration,
                BatchSize = this.BatchSize,
                CoolDownTime = this.CoolDownTime,
                SkipIf = this.SkipIf,
                TerminationRules = [.. this.TerminationRules],
                FailureRules = [.. this.FailureRules]
            };

            // Deep copy HttpRequest
            HttpRequest.DeepCopy(out HttpRequestDto? copiedHttpRequest);
            targetDto.HttpRequest = copiedHttpRequest;
        }
    }

    // Failure rule with inline operator
    /// <summary>
    /// DTO for failure rules using inline operator syntax.
    /// Example: { Metric: "ErrorRate > 0.05", ErrorStatusCodes: ">= 500" }
    /// </summary>
    public struct FailureRuleDto
    {
        /// <summary>
        /// Metric expression with inline operator.
        /// Examples: "ErrorRate > 0.05", "StatusCode >= 500", "TotalTime.P95 between 100 and 500"
        /// </summary>
        public string Metric { get; set; }

        /// <summary>
        /// For ErrorRate metrics: defines which HTTP status codes count as errors.
        /// Uses the same operator syntax as StatusCode rules.
        /// Examples: ">= 500", ">= 400", "= 401", "between 400 and 599"
        /// If not specified for ErrorRate, defaults to ">= 400" (all client and server errors).
        /// Ignored for non-ErrorRate metrics.
        /// </summary>
        public string ErrorStatusCodes { get; set; }
    }

    // Termination rule V2 with inline operator
    /// <summary>
    /// DTO for termination rules using inline operator syntax with grace period.
    /// Example: { Metric: "ErrorRate > 0.10", GracePeriod: "00:05:00", ErrorStatusCodes: ">= 500" }
    /// </summary>
    public struct TerminationRuleDto
    {
        /// <summary>
        /// Metric expression with inline operator.
        /// Examples: "ErrorRate > 0.10", "TotalTime.P90 > 1000", "StatusCode >= 500"
        /// </summary>
        public string Metric { get; set; }

        /// <summary>
        /// Grace period duration. Format: "00:05:00" (TimeSpan string)
        /// </summary>
        public string GracePeriod { get; set; }

        /// <summary>
        /// For ErrorRate metrics: defines which HTTP status codes count as errors.
        /// Uses the same operator syntax as StatusCode rules.
        /// Examples: ">= 500", ">= 400", "= 429", "between 500 and 599"
        /// If not specified for ErrorRate, defaults to ">= 400" (all client and server errors).
        /// Ignored for non-ErrorRate metrics.
        /// </summary>
        public string ErrorStatusCodes { get; set; }
    }
}
