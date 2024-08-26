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
using LPS.Domain.Domain.Common.Exceptions;
using LPS.Domain.LPSFlow;
using Newtonsoft.Json.Linq;

namespace LPS.Domain
{

    public partial class HttpRun : Run, IBusinessEntity, ICloneable
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

        private HttpRun()
        {
            Type = LPSRunType.HttpRun;
        }

        private IClientService<HttpRequestProfile, HttpResponse> _httpClientService;
        private HttpRun(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Type = LPSRunType.HttpRun;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }


        public HttpRun(SetupCommand command,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
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


        public HttpRequestProfile LPSHttpRequestProfile { get; protected set; }

        public void SetHttpRequestProfile(HttpRequestProfile lPSHttpRequestProfile)
        {
            string httpRunName = this.Name ?? string.Empty;
            LPSHttpRequestProfile = lPSHttpRequestProfile != null && lPSHttpRequestProfile.IsValid ? lPSHttpRequestProfile : throw new InvalidLPSEntityException($"In the HTTP run '{httpRunName}', the referenced LPS Entity of type {typeof(HttpRequestProfile)} is either null or invalid.");
        }

        //TODO: To be implemented
        public Flow Flow { get; protected set; }
        public int? RequestCount { get; private set; }

        public int? Duration { get; private set; }

        public int? BatchSize { get; private set; }

        public int? CoolDownTime { get; private set; }

        public IterationMode? Mode { get; private set; }
    }
}
