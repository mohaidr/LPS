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

    public partial class LPSHttpRequestProfile :LPSRequestProfile, IBusinessEntity, ICloneable
    {

        private ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> _httpClientService;
        private LPSHttpRequestProfile()
        {
        }
        ProtectedAccessLPSRunExecuteCommand _protectedCommand;
        private LPSHttpRequestProfile(ILPSLogger logger,
            ILPSWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            _protectedCommand = new ProtectedAccessLPSRunExecuteCommand();
        }

        public LPSHttpRequestProfile(LPSHttpRequestProfile.SetupCommand command, ILPSLogger logger,
            ILPSWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            HttpHeaders = new Dictionary<string, string>();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            _protectedCommand = new ProtectedAccessLPSRunExecuteCommand();
            this.Setup(command);
        }

        public int LastSequenceId { get; set; }

        public string HttpMethod { get; private set; }

        public string URL { get; private set; }

        public string Payload { get; private set; }

        public string Httpversion { get; private set; }

        public Dictionary<string, string> HttpHeaders { get; private set; }

        public bool DownloadHtmlEmbeddedResources { get; private set; }
        public bool SaveResponse { get; private set; }

    }
}
