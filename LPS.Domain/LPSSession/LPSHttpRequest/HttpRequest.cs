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
using LPS.Domain.LPSFlow.LPSHandlers;

namespace LPS.Domain
{

    public partial class HttpRequest :Request, IBusinessEntity, ICloneable
    {

        private IClientService<HttpRequest, HttpResponse> _httpClientService;
        IPlaceholderResolverService _placeholderResolverService;
        private HttpRequest()
        {
        }
        private HttpRequest(ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider, 
            IPlaceholderResolverService placeholderResolverService)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _placeholderResolverService = placeholderResolverService;
        }

        public HttpRequest(
            HttpRequest.SetupCommand command, 
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider,
            IPlaceholderResolverService placeholderResolverService)
        {
            ArgumentNullException.ThrowIfNull(command);
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _placeholderResolverService = placeholderResolverService;
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

        public CaptureHandler Capture { get; protected set; }
        //TODO: ReadHandler To Be Implemented 
        public ReadHandler Read { get; protected set; } 
    }
}
