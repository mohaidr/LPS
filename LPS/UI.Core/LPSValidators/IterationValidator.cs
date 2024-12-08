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
using LPS.DTOs;

namespace LPS.UI.Core.LPSValidators
{
    internal class IterationValidator : CommandBaseValidator<HttpIterationDto>
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
                    return name.StartsWith("$") || System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-zA-Z0-9 _.-]+$");
                })
                .WithMessage("The 'Name' must either start with '$' (for placeholders) or match the pattern: only alphanumeric characters, spaces, underscores, periods, and dashes are allowed")
                .Length(1, 60)
                .WithMessage("The 'Name' should be between 1 and 60 characters");


            RuleFor(dto => dto.StartupDelay)
                .Must(StartupDelay =>
                {

                    int.TryParse(StartupDelay, out int parsedValue);

                    return parsedValue > 0 || StartupDelay.StartsWith("$");

                }).
                WithMessage("The 'StartupDelay' must be greater than or equal to 0 or a placeholder variable");

            RuleFor(dto => dto.Mode)
                .Must(mode =>
                {
                    // Check if the mode starts with `$` (indicating a placeholder)
                    if (mode.StartsWith("$"))
                        return true;

                    // Attempt to parse the mode as an IterationMode enum value
                    return Enum.TryParse<IterationMode>(mode, out _);
                })
                .WithMessage("The 'Mode' must be a valid IterationMode (e.g., DCB, CRB, CB, R, D) or start with '$'.");


            RuleFor(dto => dto.MaximizeThroughput)
                 .Must(maximizeThroughput =>
                 {
                     return bool.TryParse(maximizeThroughput, out _) || maximizeThroughput.StartsWith("$");
                 })
                 .WithMessage("The 'MaximizeThroughput' must be a valid boolean");



            RuleFor(dto => dto.RequestCount)
                .Must(requestCount =>
                {
                    // Check if the value is a valid integer or a placeholder
                    return int.TryParse(requestCount, out int parsedValue) && parsedValue > 0 || requestCount.StartsWith("$");
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
                    if (requestCount.StartsWith("$") || dto.BatchSize.StartsWith("$"))
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
                    return int.TryParse(duration, out int parsedValue) && parsedValue > 0 || duration.StartsWith("$");
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
                    if (duration.StartsWith("$") || dto.CoolDownTime.StartsWith("$"))
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
                    return int.TryParse(batchSize, out int parsedValue) && parsedValue > 0 || batchSize.StartsWith("$");
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
                    if (batchSize.StartsWith("$") || dto.RequestCount.StartsWith("$"))
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
                    return int.TryParse(coolDownTime, out int parsedValue) && parsedValue > 0 || coolDownTime.StartsWith("$");
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
                    if (coolDownTime.StartsWith("$") || dto.Duration.StartsWith("$"))
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
        }

        public override HttpIterationDto Dto { get { return _iterationDto; } }
    }
}
