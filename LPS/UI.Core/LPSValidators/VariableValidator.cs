using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentValidation;
using LPS.UI.Common;
using LPS.Infrastructure.Logger;
using System.IO;
using LPS.UI.Common.Options;
using LPS.DTOs;

namespace LPS.UI.Core.LPSValidators
{
    internal class VariableValidator : AbstractValidator<VariableDto>
    {
        public VariableValidator()
        {
            RuleFor(variable => variable.Name)
                .NotNull().NotEmpty().WithMessage("'Variable Name' must not be empty");
            RuleFor(variable => variable.Value)
            .NotNull().NotEmpty().WithMessage("'Variable Value' must not be empty");
            RuleFor(variable => variable.As)
               .NotNull().WithMessage("'As' must be a non-null value");
            RuleFor(variable => variable.Regex)
                .NotNull().WithMessage("'Regex' must be a non-null value");
        }
    }
}
