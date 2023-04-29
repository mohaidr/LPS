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
        public class Validator: IValidator<LPSRequest, SetupCommand>
        {
            public Validator(LPSRequest entity, SetupCommand command)
            {
                Validate(entity,command);
            }

            public void Validate(LPSRequest entity, SetupCommand command)
            {
                //add validation logic if needed
            }
        }
    }
}
