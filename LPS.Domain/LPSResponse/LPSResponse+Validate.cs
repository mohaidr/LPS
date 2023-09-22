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

    public partial class LPSResponse
    {
        public class Validator: IDomainValidator<LPSResponse, SetupCommand>
        {
            ILPSLogger _logger;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            public Validator(LPSResponse entity, SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _logger = logger;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                Validate(entity, command);
            }

            public void Validate(LPSResponse entity, SetupCommand command)
            {
                command.IsValid = true;
                //add validation logic if needed
            }
        }
    }
}
