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

    public partial class LPSHttpRequest :LPSRequest
    {
        private ICustomLogger _logger;
        private LPSHttpRequest()
        {

        }

        public LPSHttpRequest(LPSHttpRequest.SetupCommand command, ICustomLogger logger )
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            this.Setup(command);
        }

        public string HttpMethod { get; private set; }

        public string URL { get; private set; }

        public string Payload { get; private set; }

        public string Httpversion { get; private set; }

        public Dictionary<string, string> HttpHeaders { get; private set; }
    }
}
