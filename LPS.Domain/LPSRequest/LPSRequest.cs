using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSRequest : IValidEntity, IExecutable, IRequestable
    {
        private ICustomLogger _logger;
        protected LPSRequest()
        {

        }

        public LPSRequest(LPSRequest.SetupCommand command, ICustomLogger logger)
        {
            _logger= logger;
        }

        public bool IsValid { get; protected set; }

        public bool HasFailed { get; protected set; }
    }

}
