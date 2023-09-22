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

    public partial class LPSHttpTestCase : LPSTestCase, IBusinessEntity
    {


        public enum IterationMode
        {
            /* 
            * D refers to Duration
            * C refers to Cool Down
            * B refers to Batch Size  
            * R refers to Request Count
            */
            DCB = 0,
            CRB = 1,
            CB = 2,
            R = 3,
            D = 4
        }

        private LPSHttpTestCase()
        {
        }

        private ILPSClientService<LPSHttpRequest> _httpClientService;
        internal LPSHttpTestCase(
            ILPSLogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider) // internal constructor should only be defined in specific scenarios where there is a need for an instance that will be setup through the command
                                                                    //This behaviour may change in the future. 
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }


        public LPSHttpTestCase(SetupCommand command,
            ILPSLogger logger, IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
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
        public LPSHttpRequest LPSHttpRequest { get; private set; }
        public int? RequestCount { get; private set; }

        public int? Duration { get; private set; }

        public int? BatchSize { get; private set; }

        public int? CoolDownTime { get; private set; }

        public IterationMode? Mode { get; private set; }
    }
}
