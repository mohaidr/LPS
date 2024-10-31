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

namespace LPS.UI.Core.LPSValidators
{
    internal class IterationValidator : CommandBaseValidator<HttpIteration.SetupCommand, HttpIteration>
    {
        HttpIteration.SetupCommand _command;

        public IterationValidator(HttpIteration.SetupCommand command)
        {
            _command = command;


            RuleFor(command => command.Name)
            .NotNull().WithMessage("The 'Name' must be a non-null value")
            .NotEmpty().WithMessage("The 'Name' must not be empty")
            .Matches("^[a-zA-Z0-9 _.-]+$")
            .WithMessage("The 'Name' does not accept special charachters")
            .Length(1, 60)
            .WithMessage("The 'Name' should be between 1 and 60 characters");

            RuleFor(command => command.Mode)
            .NotNull()
            .WithMessage("The accepted 'Mode' Values are (DCB,CRB,CB,R,D)"); 

            RuleFor(command => command.MaximizeThroughput)
            .NotNull()
            .WithMessage("The 'MaximizeThroughput' property must be a non-null value");

            RuleFor(command => command.RequestCount)
            .NotNull().WithMessage("The 'Request Count' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Request Count' must be greater than 0")
            .When(command => command.Mode == IterationMode.CRB || command.Mode == IterationMode.R)
            .Null()
            .When(command => command.Mode != IterationMode.CRB && command.Mode != IterationMode.R, ApplyConditionTo.CurrentValidator)
            .GreaterThan(command => command.BatchSize)
            .WithMessage("The 'Request Count' Must Be Greater Than The BatchSize")
            .When(command => command.Mode == IterationMode.CRB && command.BatchSize.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(command => command.Duration)
            .NotNull().WithMessage("The 'Duration' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Duration' must be greater than 0")
            .When(command => command.Mode == IterationMode.D || command.Mode == IterationMode.DCB)
            .Null()
            .When(command => command.Mode != IterationMode.D && command.Mode != IterationMode.DCB, ApplyConditionTo.CurrentValidator)
            .GreaterThan(command => command.CoolDownTime/1000)
             .WithMessage("The 'Duration*1000' Must Be Greater Than The Cool Down Time")
            .When(command => command.Mode == IterationMode.DCB && command.CoolDownTime.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(command => command.BatchSize)
            .NotNull().WithMessage("The 'Batch Size' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Batch Size' must be greater than 0")
            .When(command => command.Mode == IterationMode.DCB || command.Mode == IterationMode.CRB || command.Mode == IterationMode.CB)
            .Null()
            .When(command => command.Mode != IterationMode.DCB && command.Mode != IterationMode.CRB && command.Mode != IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(command => command.RequestCount)
            .WithMessage("The 'Batch Size' Must Be Less Than The Request Count")
            .When(command => command.Mode == IterationMode.CRB && command.RequestCount.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(command => command.CoolDownTime)
            .NotNull().WithMessage("The 'Cool Down Time' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Cool Down Time' must be greater than 0")
            .When(command => command.Mode == IterationMode.DCB || command.Mode == IterationMode.CRB || command.Mode == IterationMode.CB)
            .Null()
            .When(command => command.Mode != IterationMode.DCB && command.Mode != IterationMode.CRB && command.Mode != IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(command => command.Duration*1000)
            .WithMessage("The 'CoolDownTime/1000' Must Be Less Than The Duration")
            .When(command => command.Mode == IterationMode.DCB && command.Duration.HasValue, ApplyConditionTo.CurrentValidator);
        }

        public override HttpIteration.SetupCommand Command { get { return _command; } }
    }
}
