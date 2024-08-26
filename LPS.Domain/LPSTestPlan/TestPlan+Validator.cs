using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Validation;

namespace LPS.Domain
{

    public partial class TestPlan
    {
   
        public class Validator: CommandBaseValidator<TestPlan, TestPlan.SetupCommand>
        {

            ILogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            TestPlan _entity;
            TestPlan.SetupCommand _command;
            public override SetupCommand Command => _command;
            public override TestPlan Entity => _entity;
            public Validator(TestPlan entity, SetupCommand command, ILogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _entity = entity;
                _command = command;

                #region Validation Rules
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

                RuleFor(command => command.RampUpPeriod)
                .NotNull().WithMessage("The 'RampUp Period' must be a non-null value")
                .GreaterThan(0).When(command => command.NumberOfClients > 1)
                .WithMessage("The 'RampUp Period' must be greater than 0");


                RuleFor(command => command.DelayClientCreationUntilIsNeeded)
                .NotNull().WithMessage("'Delay Client Creation Until Is Needed' must be (y) or (n)");

                RuleFor(command => command.RunInParallel)
                .NotNull().WithMessage("'Run In Parallel' must be (y) or (n)");
                #endregion

                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Plan: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }

                _command.IsValid = base.Validate();

            }
            
        }
    }
}

