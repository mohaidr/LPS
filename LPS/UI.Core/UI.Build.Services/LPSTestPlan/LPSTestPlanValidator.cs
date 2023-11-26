using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentValidation;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestPlanValidator : LPSCommandBaseValidator<LPSTestPlan.SetupCommand, LPSTestPlan>
    {
        LPSTestPlan.SetupCommand _command;
        public LPSTestPlanValidator(LPSTestPlan.SetupCommand command)
        {
            _command = command;

            RuleFor(command => command.Name)
            .NotNull().NotEmpty()
            .Matches("^[a-zA-Z0-9 _-]+$")
            .WithMessage("The plan name does not accept special charachters and should be between 1 and 20 characters")
            .Length(1, 20)
            .WithMessage("The plan name should be between 1 and 20 characters");

            RuleFor(command => command.NumberOfClients)
            .NotNull()
            .GreaterThan(0);

            RuleFor(command => command.RampUpPeriod)
            .NotNull()
            .GreaterThan(0);

            RuleFor(command => command.DelayClientCreationUntilIsNeeded)
            .NotNull();

            RuleFor(command => command.RunInParallel)
            .NotNull();
        }

        public override LPSTestPlan.SetupCommand Command { get { return _command; } }
    }
}
