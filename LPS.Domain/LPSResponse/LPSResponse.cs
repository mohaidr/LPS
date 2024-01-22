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
    //This should be a Non-Entity Superclass
    public partial class LPSResponse : IValidEntity, ILPSResponseEntity
    {
        protected ILPSLogger _logger;
        protected ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        protected LPSResponse()
        {
            Id = Guid.NewGuid();
        }

        public LPSResponse(LPSResponse.SetupCommand command, ILPSLogger logger, ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Id = Guid.NewGuid();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
        }
        public Guid Id { get; protected set; }
        public bool IsValid { get; protected set; }
    }
}
