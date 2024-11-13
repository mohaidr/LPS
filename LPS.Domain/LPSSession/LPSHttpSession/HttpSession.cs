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

    public partial class HttpSession :Session, IBusinessEntity, ICloneable
    {

        private IClientService<HttpSession, HttpResponse> _httpClientService;
        private HttpSession()
        {
        }
        private HttpSession(ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public HttpSession(
            HttpSession.SetupCommand command, 
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            ArgumentNullException.ThrowIfNull(command);
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

        public CapturHandler Captur { get; set; }
        public StopAfterHandler StopAfter { get; set; }
        public StopIfHandler StopAIf { get; set; }
        public ReadHandler Read { get; set; }
        public PauseHandler Pause { get; set; }
    }
}
