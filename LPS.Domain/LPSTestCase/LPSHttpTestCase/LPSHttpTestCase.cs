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

    public partial class LPSHttpTestCase : LPSTestCase
    {
        private ICustomLogger _logger;

        public enum IterationMode
        {
            /* 
            * D refers to Duration
            * C refers to Cool Down
            * B refers to Batch Size  
            * R refers to Request Count
            */
            DCB,
            CRB,
            CB,
            DC,
            RC,
            R,
            D
        }

        private LPSHttpTestCase()
        {
        }

        internal LPSHttpTestCase(ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> lpsClientManager,
            ILPSClientConfiguration<LPSHttpRequest> config,
            ICustomLogger logger) // internal constructor should only be defined in specific scenarios where there is a need for an instance that will be setup through the command
                                  //This behaviour may change in the future. 
        {
            _logger = logger;
            _lpsClientManager = lpsClientManager;
            _config = config;
        }

        ILPSClientManager<LPSHttpRequest,
        ILPSClientService<LPSHttpRequest>> _lpsClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        ILPSClientService<LPSHttpRequest> _httpClient;
        public LPSHttpTestCase(SetupCommand command,
            ILPSClientManager<LPSHttpRequest,ILPSClientService<LPSHttpRequest>> lpsClientManager,
            ILPSClientConfiguration<LPSHttpRequest> config,
            ICustomLogger logger)
        {
            _logger = logger;
            _lpsClientManager = lpsClientManager;
            _config = config;
            this.Setup(command);
        }


        private int _numberOfSuccessfulCalls;
        private int _numberOfFailedCalls;

        public int NumberOfSuccessfulCalls
        {
            get => _numberOfSuccessfulCalls;
            set
            {
                if (this.IsValid)
                {
                    _numberOfSuccessfulCalls = value;
                }
            }
        }
        public int NumberOfFailedCalls
        {
            get => _numberOfFailedCalls;
            set
            {
                if (this.IsValid)
                {
                    _numberOfFailedCalls = value;
                }
            }
        }
        public LPSTestPlan Plan { get; internal set; } // internal set only for specific relationships this may change when start working in Repos and DB
        public LPSHttpRequest LPSHttpRequest { get; private set; }
        public int? RequestCount { get; private set; }

        public int? Duration { get; private set; }

        public int? BatchSize { get; private set; }

        public int? CoolDownTime { get; private set; }

        public IterationMode? Mode { get; private set; }
    }
}
