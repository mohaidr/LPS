using LPS.Domain.Common;
using System;
using System.Text.RegularExpressions;

namespace LPS.Domain
{

    public partial class LPSTestCase
    {
   
        public class Validator: IDomainValidator<LPSTestCase, SetupCommand>
        {
            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSTestCase entity, SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSTestCase entity,SetupCommand command)
            {
                command.IsValid = true;
                if (string.IsNullOrEmpty(command.Name) || !Regex.IsMatch(command.Name, @"^[\w.-]{2,}$"))
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Please Provide a Valid Name, The Name Should At least Be of 2 Charachters And Can Only Contains Letters, Numbers, ., _ and -", LPSLoggingLevel.Warning);
                    command.IsValid = false;
                }
            }
        }
    }
}

