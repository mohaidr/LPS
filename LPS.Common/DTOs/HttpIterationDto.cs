using LPS.Domain;
using System;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace LPS.UI.Common.DTOs
{
    public class HttpIterationDto : DtoBase<HttpIterationDto>
    {
        public HttpIterationDto()
        {
            Name = string.Empty;
            HttpRequest = new HttpRequestDto();
            TerminationRules = [];
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
        public string Mode { get { return string.IsNullOrWhiteSpace(_mode) ? "R" : _mode;  } set { _mode = value; } }

        // Request count (can be a variable)
        private string _requestCount;
        public string RequestCount { get { return ((string.IsNullOrWhiteSpace(_mode)  || (_mode?.Equals("R", StringComparison.OrdinalIgnoreCase) ?? false)) && string.IsNullOrWhiteSpace(_requestCount)) ? "1" : _requestCount; } set { _requestCount = value; } }

        // Duration (can be a variable)
        public string Duration { get; set; }

        // Batch size (can be a variable)
        public string BatchSize { get; set; }

        // Cooldown time (can be a variable)
        public string CoolDownTime { get; set; }
        public string MaxErrorRate { get; set; }
        public string SkipIf { get; set; }
        public string ErrorStatusCodes { get; set; } // The user should provide them as comma separated to make it easier to define variables and reslove them

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
                MaxErrorRate = this.MaxErrorRate,
                SkipIf = this.SkipIf,
                TerminationRules = [.. this.TerminationRules]
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
                        case "p90":
                            rule.P90Greater = parts[1];
                            break;
                        case "p50":
                            rule.P50Greater = parts[1];
                            break;
                        case "p10":
                            rule.P10Greater = parts[1];
                            break;
                    }
                }

                parsedRules.Add(rule);
            }

            return parsedRules;
        }
    }

    public struct TerminationRuleDto
    {
        public string ErrorStatusCodes { get; set; } // The user should provide them as comma separated to make it easier to define variables and reslove them
        public string MaxErrorRate { get; set; }
        public string GracePeriod { get; set; }
        public string P90Greater { get; set; }

        public string P50Greater { get; set; }

        public string P10Greater { get; set; }

        public string AVGGreater { get; set; }

    }
}
