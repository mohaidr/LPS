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

    public partial class LPSRequestProfile
    {
        public class Validator: IDomainValidator<LPSRequestProfile, SetupCommand>
        {
            ILPSLogger _logger;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSRequestProfile entity, SetupCommand command, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSRequestProfile entity, SetupCommand command)
            {
                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Request Profile: Entity Id Can't be Changed, The Id value will be ignored", LPSLoggingLevel.Warning);
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
