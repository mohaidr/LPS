using LPS.Domain.Common;
using System;
using System.Text.RegularExpressions;

namespace LPS.Domain
{

    public partial class LPSRun
    {
   
        public class Validator: IDomainValidator<LPSRun, SetupCommand>
        {
            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSRun entity, SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSRun entity,SetupCommand command)
            {
                command.IsValid = true;

                if (entity == null && command.Id.HasValue)
                {
                    throw new InvalidOperationException("Invalid Entity State");
                }

                if (entity.Id != default && /*command.Id.HasValue &&*/ entity.Id != command.Id)
                {
                  //  command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Run: Entity Id Can't be Changed", LPSLoggingLevel.Error);
             //       throw new InvalidOperationException("LPS Run: Entity Id Can't be Changed");
                }
            }
        }
    }
}

