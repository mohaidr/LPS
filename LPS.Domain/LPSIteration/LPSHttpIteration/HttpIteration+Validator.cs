using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using FluentValidation;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
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

            public Validator(HttpIteration entity, SetupCommand command, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _command = command;
                _entity = entity;

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

                RuleFor(c => c)
                    .Must(c =>
                    {
                        var rate = c.MaxErrorRate;
                        var codes = c.ErrorStatusCodes;

                        // If MaxErrorRate is set and > 0, ensure ErrorStatusCodes has at least one entry
                        if (rate.HasValue && rate.Value > 0)
                        {
                            return codes != null && codes.Count > 0;
                        }
                        else if (codes != null && codes.Count > 0)
                        {
                            return rate.HasValue && rate.Value > 0;
                        }

                        return true; // Valid if both are null or MaxErrorRate is 0
                    })
                    .WithMessage("If 'MaxErrorRate' is greater than 0, then 'ErrorStatusCodes' must have at least one value. If one is null, the other must be null.");


                RuleFor(c => c.TerminationRules)
                    .Must(BeValidTerminationRules)
                    .WithMessage("Termination rules must have valid HTTP status codes and a MaxErrorRate greater than 0.");
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
                if (rules == null ) return false;
                if (!rules.Any()) return true;

                return rules.All(rule =>
                    rule.ErrorStatusCodes != null && rule.MaxErrorRate != null && rule.GracePeriod != null &&
                    ((rule.ErrorStatusCodes.Count == 0 && rule.MaxErrorRate == 0 && rule.GracePeriod == TimeSpan.Zero) ||
                     (rule.MaxErrorRate > 0 &&  rule.GracePeriod> TimeSpan.Zero && rule.ErrorStatusCodes.Count > 0 &&
                     rule.ErrorStatusCodes.All(code => Enum.IsDefined(typeof(HttpStatusCode), code)))));
            }
            #endregion
        }
    }
}