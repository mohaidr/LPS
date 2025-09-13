using FluentValidation;
using LPS.UI.Common.DTOs;
using LPS.Domain.Common;
using LPS.Infrastructure.VariableServices.VariableHolders;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Extensions;

namespace LPS.UI.Core.LPSValidators
{
    internal class VariableValidator : AbstractValidator<VariableDto>
    {
        public VariableValidator()
        {
            RuleFor(variable => variable.Name)
                .NotNull().NotEmpty()
                .WithMessage("'Variable Name' must not be empty")
                .Matches("^[a-zA-Z0-9]+$")
                .WithMessage("'Variable Name' must only contain letters and numbers.");
            
            RuleFor(variable => variable.Value)
            .NotNull()
            .NotEmpty()
            .WithMessage("'Variable Value' must not be empty");

            RuleFor(variable => variable.As)
                .Must(@as =>
                {
                    return @as.TryToVariableType(out var type);
                }).WithMessage("The provided value for 'As' is not valid or supported.");
            RuleFor(variable => variable.Regex)
                .NotNull().WithMessage("'Regex' must be a non-null value");
        }
    }
}
