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

    public partial class LPSRequest : IValidEntity, IExecutable
    {
        private ICustomLogger _logger;
        private HttpClient httpClient;
        private LPSRequest()
        {

        }

        public LPSRequest(LPSRequest.SetupCommand command, ICustomLogger logger )
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            this.Setup(command);
            SocketsHttpHandler socketsHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(4),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 1,
            };
            httpClient = new HttpClient(socketsHandler);
            httpClient.Timeout = TimeSpan.FromMinutes(this.HttpRequestTimeout);
        }

        public string HttpMethod { get; private set; }

        public string URL { get; private set; }

        public string Payload { get; private set; }

        public string Httpversion { get; private set; }

        public Dictionary<string, string> HttpHeaders { get; private set; }

        public int HttpRequestTimeout { get; private set; }

        public bool IsValid { get; private set; }

        public bool HasFailed { get; private set; }
    }

}
