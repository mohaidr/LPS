using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentValidation;
using LPS.DTOs;

namespace LPS.UI.Core.LPSValidators
{
    internal class PlanValidator : CommandBaseValidator<PlanDto, Plan>
    {
        readonly PlanDto _planDto;
        public PlanValidator(PlanDto planDto)
        {
            ArgumentNullException.ThrowIfNull(planDto);
            _planDto = planDto;

            RuleFor(command => command.Name)
            .NotNull().WithMessage("The 'Name' must be a non-null value")
            .NotEmpty().WithMessage("The 'Name' must not be empty")
            .Matches("^[a-zA-Z0-9 _.-]+$")
            .WithMessage("The 'Name' does not accept special charachters")
            .Length(1, 60)
            .WithMessage("The 'Name' should be between 1 and 60 characters");

            RuleFor(command => command.Rounds)
                .Must(HaveUniqueRoundNames)
                .WithMessage("The Round 'Name' must be unique.");
            RuleFor(command => command.Iterations)
               .Must(HaveUniqueIterationNames)
               .WithMessage("The Round 'Name' must be unique.");
        }
        private bool HaveUniqueRoundNames(IList<RoundDto> rounds)
        {
            if (rounds == null) return true;

            // Check for duplicate names in the provided rounds list
            var roundNames = rounds.Select(round => round.Name).ToList();
            return roundNames.Count == roundNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }

        private bool HaveUniqueIterationNames(IList<HttpIterationDto> iterations)
        {
            if (iterations == null) return true;

            // Check for duplicate names in the provided rounds list
            var iterationsNames = iterations.Select(iteration => iteration.Name).ToList();
            return iterationsNames.Count == iterationsNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
        public override PlanDto Dto { get { return _planDto; } }
    }
}
