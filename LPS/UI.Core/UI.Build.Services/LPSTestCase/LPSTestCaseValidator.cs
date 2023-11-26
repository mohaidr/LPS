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
    internal class LPSTestCaseValidator : LPSCommandBaseValidator<LPSHttpTestCase.SetupCommand, LPSHttpTestCase>
    {
        LPSHttpTestCase.SetupCommand _command;

        public LPSTestCaseValidator(LPSHttpTestCase.SetupCommand command)
        {
            _command = command;

            RuleFor(command => command.Name)
            .NotNull().NotEmpty()
            .Matches("^[a-zA-Z0-9 _-]+$")
            .WithMessage("The test case name does not accept special charachters and should be between 1 and 20 characters")
            .Length(1, 20)
            .WithMessage("The test case should be between 1 and 20 characters");

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
            .WithMessage("Request Count Must Be Greater Than The BatchSize")
            .When(command => command.BatchSize.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(command => command.Duration)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Mode == LPSHttpTestCase.IterationMode.D || command.Mode == LPSHttpTestCase.IterationMode.DCB)
            .Null()
            .When(command => command.Mode != LPSHttpTestCase.IterationMode.D && command.Mode != LPSHttpTestCase.IterationMode.DCB, ApplyConditionTo.CurrentValidator)
            .GreaterThan(command => command.CoolDownTime)
             .WithMessage("Duration Must Be Greater Than The Cool Down Time")
            .When(command => command.CoolDownTime.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(command => command.BatchSize)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Mode == LPSHttpTestCase.IterationMode.DCB || command.Mode == LPSHttpTestCase.IterationMode.CRB || command.Mode == LPSHttpTestCase.IterationMode.CB)
            .Null()
            .When(command => command.Mode != LPSHttpTestCase.IterationMode.DCB && command.Mode != LPSHttpTestCase.IterationMode.CRB && command.Mode != LPSHttpTestCase.IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(command => command.RequestCount)
            .WithMessage("Batch Size Must Be Less Than The Request Count")
            .When(command => command.RequestCount.HasValue, ApplyConditionTo.CurrentValidator);           

            RuleFor(command => command.CoolDownTime)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Mode == LPSHttpTestCase.IterationMode.DCB || command.Mode == LPSHttpTestCase.IterationMode.CRB || command.Mode == LPSHttpTestCase.IterationMode.CB)
            .Null()
            .When(command => command.Mode != LPSHttpTestCase.IterationMode.DCB && command.Mode!= LPSHttpTestCase.IterationMode.CRB && command.Mode != LPSHttpTestCase.IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(command => command.Duration)
            .WithMessage("Cool Down Time Must Be Less Than The Duration")
            .When(command => command.Duration.HasValue, ApplyConditionTo.CurrentValidator);
        }

        public override LPSHttpTestCase.SetupCommand Command { get { return _command; } }
    }

}
