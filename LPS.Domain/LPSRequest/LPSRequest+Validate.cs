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

    public partial class LPSRequest
    {
        public class Validator: IDomainValidator<LPSRequest, SetupCommand>
        {
            ILPSLogger _logger;
            public Validator(LPSRequest entity, SetupCommand command, ILPSLogger logger)
            {
                Validate(entity, command);
                _logger = logger;

            }

            public void Validate(LPSRequest entity, SetupCommand command)
            {
                //add validation logic if needed
            }
        }
    }
}
