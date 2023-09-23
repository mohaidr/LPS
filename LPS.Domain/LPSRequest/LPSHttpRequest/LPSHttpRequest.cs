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

    public partial class LPSHttpRequest :LPSRequest, IBusinessEntity
    {

        private ILPSClientService<LPSHttpRequest> _httpClientService;
        private LPSHttpRequest()
        {
            LPSHttpResponses = new List<LPSHttpResponse>();
        }

        internal LPSHttpRequest(ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            LPSHttpResponses = new List<LPSHttpResponse>();
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public LPSHttpRequest(LPSHttpRequest.SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            this.Setup(command);
        }
        public string HttpMethod { get; private set; }

        public string URL { get; private set; }

        public string Payload { get; private set; }

        public string Httpversion { get; private set; }

        public Dictionary<string, string> HttpHeaders { get; private set; }

        //Same request might be executed multiple times so may have different response for each execution.
        public List<LPSHttpResponse> LPSHttpResponses { get; private set; }

        public bool DownloadHtmlEmbeddedResources { get; private set; }
    }
}
