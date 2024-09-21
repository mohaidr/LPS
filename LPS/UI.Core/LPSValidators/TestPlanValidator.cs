using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FluentValidation;

namespace LPS.UI.Core.LPSValidators
{
    internal class TestPlanValidator : CommandBaseValidator<TestPlan.SetupCommand, TestPlan>
    {
        TestPlan.SetupCommand _command;
        public TestPlanValidator(TestPlan.SetupCommand command)
        {
            _command = command;

            RuleFor(command => command.Name)
            .NotNull().WithMessage("The 'Name' must be a non-null value")
            .NotEmpty().WithMessage("The 'Name' must not be empty")
            .Matches("^[a-zA-Z0-9 _-]+$")
            .WithMessage("The 'Name' does not accept special charachters")
            .Length(1, 20)
            .WithMessage("The 'Name' should be between 1 and 20 characters");

            RuleFor(command => command.NumberOfClients)
            .NotNull().WithMessage("The 'Number Of Clients' must be a non-null value")
            .GreaterThan(0).WithMessage("The 'Number Of Clients' must be greater than 0");

            RuleFor(command => command.ArrivalDelay)
            .NotNull().WithMessage("The 'Arrival Delay' must be a non-null value")
            .GreaterThan(0).When(command=> command.NumberOfClients>1)
            .WithMessage("The 'Arrival Delay' must be greater than 0");


            RuleFor(command => command.DelayClientCreationUntilIsNeeded)
            .NotNull().WithMessage("'Delay Client Creation Until Is Needed' must be (y) or (n)");

            RuleFor(command => command.RunInParallel)
            .NotNull().WithMessage("'Run In Parallel' must be (y) or (n)");
        }

        public override TestPlan.SetupCommand Command { get { return _command; } }
    }
}
