using LPS.Domain.Common.Interfaces;
using System;
using System.Text.RegularExpressions;

namespace LPS.Domain
{

    public partial class LPSRun
    {
   
        public class Validator: IDomainValidator<LPSRun, SetupCommand>
        {
            ILPSLogger _logger;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSRun entity, SetupCommand command, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSRun entity,SetupCommand command)
            {
                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Run: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
                }
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Entity", LPSLoggingLevel.Warning);
                    throw new ArgumentNullException(nameof(entity));
                }

                if (command == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Invalid Entity Command", LPSLoggingLevel.Warning);
                    throw new ArgumentNullException(nameof(command));
                }
                command.IsValid = true;
            }
        }
    }
}

