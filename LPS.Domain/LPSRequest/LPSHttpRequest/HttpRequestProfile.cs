using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class HttpRequestProfile :RequestProfile, IBusinessEntity, ICloneable
    {

        private IClientService<HttpRequestProfile, HttpResponse> _httpClientService;
        private HttpRequestProfile()
        {
        }
        private HttpRequestProfile(ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public HttpRequestProfile(
            HttpRequestProfile.SetupCommand command, 
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            this.Setup(command);
        }

        public int LastSequenceId { get; protected set; }

        public string HttpMethod { get; protected set; }

        public string URL { get; protected set; }

        public string Payload { get; protected set; }

        public string HttpVersion { get; protected set; }

        public Dictionary<string, string> HttpHeaders { get; protected set; }

        public bool DownloadHtmlEmbeddedResources { get; protected set; }

        public bool? SupportH2C { get; protected set; }

        public bool SaveResponse { get; protected set; }
    }
}
