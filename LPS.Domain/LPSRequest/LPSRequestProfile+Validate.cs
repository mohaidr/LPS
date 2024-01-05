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
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSRequestProfile
    {
        public class Validator: IDomainValidator<LPSRequestProfile, SetupCommand>
        {
            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSRequestProfile entity, SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSRequestProfile entity, SetupCommand command)
            {
                command.IsValid = true;
                if (entity.Id != default && command.Id.HasValue && entity.Id != command.Id)
                {
                   // command.IsValid = false;
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPS Request Profile: Entity Id Can't be Changed", LPSLoggingLevel.Error);
                   // throw new InvalidOperationException("LPS Request Profile: Entity Id Can't be Changed");
                }
            }
        }
    }
}
