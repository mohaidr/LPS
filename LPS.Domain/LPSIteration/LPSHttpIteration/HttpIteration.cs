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
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Exceptions;
using LPS.Domain.LPSFlow;
using Newtonsoft.Json.Linq;

namespace LPS.Domain
{
    public partial class HttpIteration : Iteration, IBusinessEntity, ICloneable
    {

        private HttpIteration()
        {
            Type = IterationType.Http;
        }

        private IClientService<HttpRequestProfile, HttpResponse> _httpClientService;
        private HttpIteration(
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Type = IterationType.Http;
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }


        public HttpIteration(SetupCommand command,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Type = IterationType.Http;
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


        public HttpRequestProfile RequestProfile { get; protected set; }

        public void SetHttpRequestProfile(HttpRequestProfile lPSHttpRequestProfile)
        {
            string httpIterationName = this.Name ?? string.Empty;
            RequestProfile = lPSHttpRequestProfile != null && lPSHttpRequestProfile.IsValid ? lPSHttpRequestProfile : throw new InvalidLPSEntityException($"In the HTTP iteration '{httpIterationName}', the referenced LPS Entity of type {typeof(HttpRequestProfile)} is either null or invalid.");
        }

        //TODO: To be implemented
        public Flow Flow { get; protected set; }
        public int? RequestCount { get; private set; }

        public int? Duration { get; private set; }

        public int? BatchSize { get; private set; }

        public int? CoolDownTime { get; private set; }

        public IterationMode? Mode { get; private set; }
        public bool MaximizeThroughput { get; private set; }

        public ExecutionStatus Status { get; private set; }
    }
}
