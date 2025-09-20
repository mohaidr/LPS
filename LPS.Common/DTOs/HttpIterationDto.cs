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
            FailureCriteria = new FailureCriteriaDto();
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

        // New: consolidated failure criteria (replaces legacy MaxErrorRate/ErrorStatusCodes on DTO)
        public FailureCriteriaDto FailureCriteria { get; set; }

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
                FailureCriteria = this.FailureCriteria // value-copy for struct; for class, shallow copy is fine
            };

            // Deep copy HttpRequest
            HttpRequest.DeepCopy(out HttpRequestDto? copiedHttpRequest);
            targetDto.HttpRequest = copiedHttpRequest;
        }

        public static List<TerminationRuleDto> ParseTerminationRules(IEnumerable<string> ruleInputs)
        {
            var parsedRules = new List<TerminationRuleDto>();

            foreach (var input in ruleInputs)
            {
                var rule = new TerminationRuleDto();
                var segments = input.Split(';', StringSplitOptions.RemoveEmptyEntries);

                foreach (var segment in segments)
                {
                    var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length != 2) continue;

                    switch (parts[0].ToLower())
                    {
                        case "codes":
                            rule.ErrorStatusCodes = parts[1];
                            break;
                        case "rate":
                            rule.MaxErrorRate = parts[1];
                            break;
                        case "grace":
                            rule.GracePeriod = parts[1];
                            break;
                        case "maxp90":
                        case "p90":
                            rule.MaxP90 = parts[1];
                            break;
                        case "maxp50":
                        case "p50":
                            rule.MaxP50 = parts[1];
                            break;
                        case "maxp10":
                        case "p10":
                            rule.MaxP10 = parts[1];
                            break;
                        case "avg":
                        case "maxavg":
                            rule.MaxAvg = parts[1];
                            break;
                    }
                }

                parsedRules.Add(rule);
            }

            return parsedRules;
        }
        public static FailureCriteriaDto ParseFailureCriteria(string? input)
        {
            var fc = new FailureCriteriaDto();

            if (string.IsNullOrWhiteSpace(input))
                return fc;

            var segments = input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var segment in segments)
            {
                // accept tokens with or without '=' (e.g., "maxp90=300" or "maxp90=300ms" if your resolver supports units)
                var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
                var key = parts[0].ToLowerInvariant();
                var val = parts.Length == 2 ? parts[1] : string.Empty;

                switch (key)
                {
                    case "codes":
                        fc.ErrorStatusCodes = val;
                        break;
                    case "rate":
                        fc.MaxErrorRate = val;
                        break;
                    case "maxp90":
                    case "p90":
                        fc.MaxP90 = val;
                        break;
                    case "maxp50":
                    case "p50":
                        fc.MaxP50 = val;
                        break;
                    case "maxp10":
                    case "p10":
                        fc.MaxP10 = val;
                        break;
                    case "avg":
                    case "maxavg":
                        fc.MaxAvg = val;
                        break;
                }
            }

            return fc;
        }
    }

    // New DTO for FailureCriteria (string-based for variable support)
    public struct FailureCriteriaDto
    {
        // If provided, must pair with ErrorStatusCodes (or both placeholders)
        public string MaxErrorRate { get; set; }
        public string ErrorStatusCodes { get; set; } // comma-separated or placeholder

        // Optional latency thresholds (ms)
        public string MaxP90 { get; set; }
        public string MaxP50 { get; set; }
        public string MaxP10 { get; set; }
        public string MaxAvg { get; set; }
    }

    public struct TerminationRuleDto
    {
        public string ErrorStatusCodes { get; set; } // The user should provide them as comma separated to make it easier to define variables and resolve them
        public string MaxErrorRate { get; set; }
        public string GracePeriod { get; set; }
        public string MaxP90 { get; set; }
        public string MaxP50 { get; set; }
        public string MaxP10 { get; set; }
        public string MaxAvg { get; set; }
    }
}
