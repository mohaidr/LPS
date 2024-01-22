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
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
   
        public class Validator: IDomainValidator<LPSTestPlan, LPSTestPlan.SetupCommand>
        {

            ILPSLogger _logger;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSTestPlan entity, SetupCommand command, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSTestPlan entity,SetupCommand command)
            {

                if (entity == null)
                {
                    throw new InvalidOperationException("Invalid Entity State");
                }

                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Plan: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }

                command.IsValid = true;

                if (string.IsNullOrEmpty(command.Name)  || !Regex.IsMatch(command.Name, @"^[\w.-]{2,}$"))
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Test Name, The name should at least be of 2 charachters and can only contain letters, numbers, ., _ and -", LPSLoggingLevel.Warning);
                }
                if (command.NumberOfClients < 1)
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Number of clients can't be less than 1, at least one user has to be created.", LPSLoggingLevel.Warning);
                }

                if (!command.DelayClientCreationUntilIsNeeded.HasValue) 
                { 
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Delay client creation until needed must have a value.", LPSLoggingLevel.Warning);

                }

                if (!command.RunInParallel.HasValue)
                {
                    command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Run in parallel must have a value.", LPSLoggingLevel.Warning);

                }
              
            }
        }
    }
}

