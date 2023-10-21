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
    //This should be a Non-Entity Superclass
    public partial class LPSRequest : IValidEntity, ILPSRequestEntity
    {
        protected ILPSLogger _logger;
        protected IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        protected ILPSWatchdog _watchdog;
        protected LPSRequest()
        {

        }

        public LPSRequest(LPSRequest.SetupCommand command, ILPSLogger logger,
            ILPSWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            _logger= logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
        }
        public Guid Id { get; protected set; }

        public bool IsValid { get; protected set; }

        public bool HasFailed { get; protected set; }
    }

}
