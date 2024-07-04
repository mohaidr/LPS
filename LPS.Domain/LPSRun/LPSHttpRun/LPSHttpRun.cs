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

    public partial class LPSHttpRun : LPSRun, IBusinessEntity, ICloneable
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

        private LPSHttpRun()
        {
            Type = LPSRunType.HttpRun;
        }

        private ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> _httpClientService;
        private LPSHttpRun(
            ILPSLogger logger,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Type = LPSRunType.HttpRun;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }


        public LPSHttpRun(SetupCommand command,
            ILPSLogger logger,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Type = LPSRunType.HttpRun;
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

        LPSHttpRequestProfile _lpsHttpRequestProfile;

        //This should not be allowed to be set explicitly.
        public LPSHttpRequestProfile LPSHttpRequestProfile 
        { 
            get 
            { 
                return _lpsHttpRequestProfile; 
            } 
            set 
            { 
                _lpsHttpRequestProfile = value!=null && value.IsValid ? value: _lpsHttpRequestProfile;
            } 
        }
        public int? RequestCount { get; private set; }

        public int? Duration { get; private set; }

        public int? BatchSize { get; private set; }

        public int? CoolDownTime { get; private set; }

        public IterationMode? Mode { get; private set; }
    }
}
