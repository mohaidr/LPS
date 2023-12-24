using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSHttpResponse :LPSResponse, IBusinessEntity
    {

        private LPSHttpResponse()
        {
        }


        internal LPSHttpResponse(ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }

        public LPSHttpResponse(LPSHttpResponse.SetupCommand command, ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            this.Setup(command);
        }

        public MimeType ContentType { get; private set; }
        public string LocationToResponse { get; private set; }
        public HttpStatusCode StatusCode { get; private set; }
        public string StatusMessage { get; private set; }
        public Dictionary<string, string> ResponseContentHeaders { get; private set; }
        public Dictionary<string, string> ResponseHeaders { get; private set; }
        public bool IsSuccessStatusCode { get; private set; }
        public LPSHttpRequestProfile LPSHttpRequestProfile { get; private set; }
    }
}
