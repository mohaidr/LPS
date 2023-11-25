using LPS.Domain;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using FluentValidation;
using LPS.UI.Common;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace LPS.UI.Core.UI.Build.Services
{
    internal class LPSTestCaseValidator : LPSBaseValidator<LPSHttpTestCase.SetupCommand, LPSHttpTestCase>
    {
        LPSHttpTestCase.SetupCommand _command;

        public LPSTestCaseValidator(LPSHttpTestCase.SetupCommand command)
        {
            _command = command;

            RuleFor(command => command.Name)
            .NotEmpty()
            .Length(1, 20)
            .WithMessage("The Test Case Name Should be between 1 and 20 characters");

            RuleFor(command => command.Mode)
            .NotNull()
            .WithMessage("Iteration Mode can't be null");

            RuleFor(command => command.RequestCount)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Mode == LPSHttpTestCase.IterationMode.CRB || command.Mode == LPSHttpTestCase.IterationMode.R)
            .Null()
            .When(command => command.Mode != LPSHttpTestCase.IterationMode.CRB && command.Mode != LPSHttpTestCase.IterationMode.R, ApplyConditionTo.CurrentValidator)
            .GreaterThan(command => command.BatchSize)
            .When(command => command.BatchSize.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(command => command.Duration)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Mode == LPSHttpTestCase.IterationMode.D || command.Mode == LPSHttpTestCase.IterationMode.DCB)
            .Null()
            .When(command => command.Mode != LPSHttpTestCase.IterationMode.D && command.Mode != LPSHttpTestCase.IterationMode.DCB, ApplyConditionTo.CurrentValidator)
            .GreaterThan(command => command.CoolDownTime)
            .When(command => command.CoolDownTime.HasValue, ApplyConditionTo.CurrentValidator);


            RuleFor(command => command.BatchSize)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Mode == LPSHttpTestCase.IterationMode.DCB || command.Mode == LPSHttpTestCase.IterationMode.CRB || command.Mode == LPSHttpTestCase.IterationMode.CB)
            .Null()
            .When(command => command.Mode != LPSHttpTestCase.IterationMode.DCB && command.Mode != LPSHttpTestCase.IterationMode.CRB && command.Mode != LPSHttpTestCase.IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(command => command.RequestCount)
            .When(command => command.RequestCount.HasValue, ApplyConditionTo.CurrentValidator);
            Console.WriteLine(_command.Duration);
           

            RuleFor(command => command.CoolDownTime)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Mode == LPSHttpTestCase.IterationMode.DCB || command.Mode == LPSHttpTestCase.IterationMode.CRB || command.Mode == LPSHttpTestCase.IterationMode.CB)
            .Null()
            .When(command => command.Mode != LPSHttpTestCase.IterationMode.DCB && command.Mode!= LPSHttpTestCase.IterationMode.CRB && command.Mode != LPSHttpTestCase.IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(command => command.Duration)
            .When(command => command.Duration.HasValue, ApplyConditionTo.CurrentValidator);
        }

        public override LPSHttpTestCase.SetupCommand Command { get { return _command; } }
    }

}
