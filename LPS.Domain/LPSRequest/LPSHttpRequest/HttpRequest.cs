#nullable enable
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
using LPS.Domain.LPSRequest.LPSHttpRequest;
using LPS.Domain.LPSSession;

namespace LPS.Domain
{

    public sealed class RetryPolicy
    {
        public string If { get; set; }
        public string StopIf { get; set; }
        public int? MaxRetries { get; set; }
        public int? BaseDelayInMs { get; set; }
        public int? MaxDelayInMs { get; set; }
    }

    public partial class HttpRequest :Request, IBusinessEntity, ICloneable
    {

        private HttpRequest()
        {
        }
        private HttpRequest(ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public HttpRequest(
            HttpRequest.SetupCommand command, 
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

        public URL Url { get; protected set; }

        public Payload? Payload { get; protected set; }

        public string HttpVersion { get; protected set; }

        public string SkipIf { get; protected set; }

        public RetryPolicy Retry { get; protected set; }

        public Dictionary<string, string> HttpHeaders { get; protected set; }

        public bool DownloadHtmlEmbeddedResources { get; protected set; }

        public bool? SupportH2C { get; protected set; }

        public bool SaveResponse { get; protected set; }

        public string? ClientCertificatePath { get; protected set; }

        public string? ClientCertificatePassword { get; protected set; }

        public CaptureHandler Capture { get; protected set; }
        //TODO: ReadHandler To Be Implemented 
    }
}
