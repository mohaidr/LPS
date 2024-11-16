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
using LPS.DTOs;

namespace LPS.UI.Core.LPSValidators
{
    internal class IterationValidator : CommandBaseValidator<HttpIterationDto, HttpIteration>
    {
        readonly HttpIterationDto _iterationDto;
        public IterationValidator(HttpIterationDto iterationDto)
        {
            ArgumentNullException.ThrowIfNull(iterationDto);
            _iterationDto = iterationDto;


            RuleFor(dto => dto.Name)
            .NotNull().WithMessage("The 'Name' must be a non-null value")
            .NotEmpty().WithMessage("The 'Name' must not be empty")
            .Matches("^[a-zA-Z0-9 _.-]+$")
            .WithMessage("The 'Name' does not accept special charachters")
            .Length(1, 60)
            .WithMessage("The 'Name' should be between 1 and 60 characters");

            RuleFor(dto => dto.Mode)
            .NotNull()
            .WithMessage("The accepted 'Mode' Values are (DCB,CRB,CB,R,D)"); 

            RuleFor(dto => dto.MaximizeThroughput)
            .NotNull()
            .WithMessage("The 'MaximizeThroughput' property must be a non-null value");

            RuleFor(dto => dto.RequestCount)
            .NotNull().WithMessage("The 'Request Count' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Request Count' must be greater than 0")
            .When(dto => dto.Mode == IterationMode.CRB || dto.Mode == IterationMode.R)
            .Null()
            .When(dto => dto.Mode != IterationMode.CRB && dto.Mode != IterationMode.R, ApplyConditionTo.CurrentValidator)
            .GreaterThan(dto => dto.BatchSize)
            .WithMessage("The 'Request Count' Must Be Greater Than The BatchSize")
            .When(dto => dto.Mode == IterationMode.CRB && dto.BatchSize.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(dto => dto.Duration)
            .NotNull().WithMessage("The 'Duration' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Duration' must be greater than 0")
            .When(dto => dto.Mode == IterationMode.D || dto.Mode == IterationMode.DCB)
            .Null()
            .When(dto => dto.Mode != IterationMode.D && dto.Mode != IterationMode.DCB, ApplyConditionTo.CurrentValidator)
            .GreaterThan(dto => dto.CoolDownTime/1000)
             .WithMessage("The 'Duration*1000' Must Be Greater Than The Cool Down Time")
            .When(dto => dto.Mode == IterationMode.DCB && dto.CoolDownTime.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(dto => dto.BatchSize)
            .NotNull().WithMessage("The 'Batch Size' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Batch Size' must be greater than 0")
            .When(dto => dto.Mode == IterationMode.DCB || dto.Mode == IterationMode.CRB || dto.Mode == IterationMode.CB)
            .Null()
            .When(dto => dto.Mode != IterationMode.DCB && dto.Mode != IterationMode.CRB && dto.Mode != IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(dto => dto.RequestCount)
            .WithMessage("The 'Batch Size' Must Be Less Than The Request Count")
            .When(dto => dto.Mode == IterationMode.CRB && dto.RequestCount.HasValue, ApplyConditionTo.CurrentValidator);

            RuleFor(dto => dto.CoolDownTime)
            .NotNull().WithMessage("The 'Cool Down Time' must be a non-null value and greater than 0")
            .GreaterThan(0).WithMessage("The 'Cool Down Time' must be greater than 0")
            .When(dto => dto.Mode == IterationMode.DCB || dto.Mode == IterationMode.CRB || dto.Mode == IterationMode.CB)
            .Null()
            .When(dto => dto.Mode != IterationMode.DCB && dto.Mode != IterationMode.CRB && dto.Mode != IterationMode.CB, ApplyConditionTo.CurrentValidator)
            .LessThan(dto => dto.Duration*1000)
            .WithMessage("The 'CoolDownTime/1000' Must Be Less Than The Duration")
            .When(dto => dto.Mode == IterationMode.DCB && dto.Duration.HasValue, ApplyConditionTo.CurrentValidator);
        }

        public override HttpIterationDto Dto { get { return _iterationDto; } }
    }
}
