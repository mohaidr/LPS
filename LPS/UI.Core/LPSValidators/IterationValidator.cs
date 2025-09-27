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

namespace LPS.UI.Core.LPSValidators
{
    internal partial class IterationValidator : CommandBaseValidator<HttpIterationDto>
    {
        readonly HttpIterationDto _iterationDto;
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
                .Must(BeValidTerminationRules).WithMessage("Ivalid Termination Rule");
            
            RuleFor(dto => dto.FailureCriteria)
                .Must(fc =>
                {
                    // If everything is empty -> valid (no failure checks configured)
                    bool hasAnyThreshold =
                        IsPositiveNumberOrPlaceholder(fc.MaxErrorRate) ||
                        IsPositiveNumberOrPlaceholder(fc.MaxP90) ||
                        IsPositiveNumberOrPlaceholder(fc.MaxP50) ||
                        IsPositiveNumberOrPlaceholder(fc.MaxP10) ||
                        IsPositiveNumberOrPlaceholder(fc.MaxAvg);

                    if (!hasAnyThreshold && string.IsNullOrWhiteSpace(fc.ErrorStatusCodes))
                        return true;

                    // Error-rate pairing: if one is provided, the other must be provided too
                    bool hasRate = IsPositiveNumberOrPlaceholder(fc.MaxErrorRate);
                    bool hasCodes = !string.IsNullOrWhiteSpace(fc.ErrorStatusCodes);

                    if (hasRate ^ hasCodes) // exactly one present -> invalid
                        return false;

                    // If codes are provided, they must be placeholder or at least one valid HTTP status code int
                    if (hasCodes && !fc.ErrorStatusCodes.StartsWith("$"))
                    {
                        var parts = fc.ErrorStatusCodes
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim());

                        if (!parts.Any()) return false;

                        // accept either numeric codes or named codes (e.g., "InternalServerError")
                        bool anyValid = parts.Any(code =>
                            int.TryParse(code, out var numeric) && Enum.IsDefined(typeof(HttpStatusCode), numeric) ||
                            Enum.TryParse<HttpStatusCode>(code, true, out _));

                        if (!anyValid) return false;
                    }

                    // Duration thresholds: placeholders or numeric > 0 are acceptable
                    // No extra cross-field constraints required
                    return true;
                })
                .WithMessage("FailureCriteria is invalid: If MaxErrorRate is provided, ErrorStatusCodes must also be provided (and vice versa). Status codes must be a placeholder or valid HTTP codes.");


            RuleFor(dto => dto.HttpRequest)
                .SetValidator(new RequestValidator(new HttpRequestDto()));

        }


        private bool BeValidTerminationRules(IEnumerable<TerminationRuleDto> rules)
        {
            if (rules == null) return false;
            if (!rules.Any()) return true;

            return rules.All
             (
                 rule =>
                 {
                     bool isErrorCriteriaEmpty = string.IsNullOrWhiteSpace(rule.MaxErrorRate)
                                                 && string.IsNullOrWhiteSpace(rule.ErrorStatusCodes);

                     bool isLatencyCriteriaEmpty = string.IsNullOrWhiteSpace(rule.MaxAvg)
                                                      && string.IsNullOrWhiteSpace(rule.MaxP10)
                                                      && string.IsNullOrWhiteSpace(rule.MaxP50)
                                                      && string.IsNullOrWhiteSpace(rule.MaxP90);

                     bool isGracePeriodEmpty = string.IsNullOrWhiteSpace(rule.GracePeriod);

                     if ((isErrorCriteriaEmpty && isLatencyCriteriaEmpty) || isGracePeriodEmpty)
                         return false;
                     bool validCodes = false;
                     if (!isErrorCriteriaEmpty)
                     {
                         var codes = rule.ErrorStatusCodes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList();
                         validCodes = rule.ErrorStatusCodes.StartsWith("$") || (codes.All(code => Enum.TryParse<HttpStatusCode>(code, out _)) && codes.Count > 0);
                     }
                     var validGrace = rule.GracePeriod.StartsWith("$") || (TimeSpan.TryParse(rule.GracePeriod, out var gracePeriod) && gracePeriod > TimeSpan.Zero);
                     var validRate = (!string.IsNullOrWhiteSpace(rule.MaxErrorRate) && rule.MaxErrorRate.StartsWith("$")) || (double.TryParse(rule.MaxErrorRate, out var maxErrorRate) && maxErrorRate > 0);
                     var validP90 = (!string.IsNullOrWhiteSpace(rule.MaxP90) && rule.MaxP90.StartsWith("$")) || (double.TryParse(rule.MaxP90, out var p90Greater) && p90Greater > 0);
                     var validP10 = (!string.IsNullOrWhiteSpace(rule.MaxP10) && rule.MaxP10.StartsWith("$")) || (double.TryParse(rule.MaxP10, out var p10Greater) && p10Greater > 0);
                     var validP50 = (!string.IsNullOrWhiteSpace(rule.MaxP50) && rule.MaxP50.StartsWith("$")) || (double.TryParse(rule.MaxP50, out var p50Greater) && p50Greater > 0);
                     var validAVG = (!string.IsNullOrWhiteSpace(rule.MaxAvg) && rule.MaxAvg.StartsWith("$")) || (double.TryParse(rule.MaxAvg, out var avgGreater) && avgGreater > 0);

                     return (validCodes  || (validRate || validAVG || validP90 || validP50 || validP10)) && validGrace;

                 }
             );


        }

        private static bool IsPositiveNumberOrPlaceholder(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (s.StartsWith("$")) return true;
            return double.TryParse(s, out var d) && d > 0;
        }



        public override HttpIterationDto Dto { get { return _iterationDto; } }

        [GeneratedRegex("^[a-zA-Z0-9 _.-]+$")]
        private static partial Regex NameRegex();
    }
}
