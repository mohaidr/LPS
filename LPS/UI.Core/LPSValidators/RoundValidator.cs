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
    internal class RoundValidator : CommandBaseValidator<RoundDto, Round>
    {
        readonly RoundDto _roundDto;
        public RoundValidator(RoundDto roundDto)
        {
            ArgumentNullException.ThrowIfNull(roundDto);

            _roundDto = roundDto;

            RuleFor(dto => dto.Name)
            .NotNull().WithMessage("The 'Name' must be a non-null value")
            .NotEmpty().WithMessage("The 'Name' must not be empty")
            .Matches("^[a-zA-Z0-9 _.-]+$")
            .WithMessage("The 'Name' does not accept special charachters")
            .Length(1, 60)
            .WithMessage("The 'Name' should be between 1 and 60 characters");

            RuleFor(dto => dto.BaseUrl)
            .Must(url =>
            {
                return Uri.TryCreate(url, UriKind.Absolute, out Uri result)
                && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
            })
            .When(dto=> !string.IsNullOrEmpty(dto.BaseUrl))
            .WithMessage("The 'BaseUrl' must be a valid URL according to RFC 3986");


            RuleFor(dto=> dto.Iterations)
            .Must(HaveUniqueIterationNames)
            .WithMessage("The Iteration 'Name' must be unique.");

            RuleFor(dto => dto.StartupDelay)
            .GreaterThanOrEqualTo(0)
            .WithMessage("The 'StartUpDelay' must be greater than or equal to 0");

            RuleFor(dto => dto.NumberOfClients)
            .NotNull().WithMessage("The 'Number Of Clients' must be a non-null value")
            .GreaterThan(0).WithMessage("The 'Number Of Clients' must be greater than 0");

            RuleFor(dto => dto.ArrivalDelay)
            .NotNull().WithMessage("The 'Arrival Delay' must be greater than 0")
            .GreaterThan(0)
            .When(dto=> dto.NumberOfClients>1)
            .WithMessage("The 'Arrival Delay' must be greater than 0");


            RuleFor(dto => dto.DelayClientCreationUntilIsNeeded)
            .NotNull().WithMessage("'Delay Client Creation Until Is Needed' must be (y) or (n)");

            RuleFor(dto => dto.RunInParallel)
            .NotNull().WithMessage("'Run In Parallel' must be (y) or (n)");
        }
        private bool HaveUniqueIterationNames(IList<HttpIterationDto> iterations)
        {
            if (iterations == null) return true;

            // Check for duplicate names in the provided rounds list
            var iterationsNames = iterations.Select(iteration => iteration.Name).ToList();
            return iterationsNames.Count == iterationsNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
        public override RoundDto Dto { get { return _roundDto; } }
    }
}
