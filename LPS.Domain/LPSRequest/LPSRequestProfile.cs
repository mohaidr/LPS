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
    public partial class LPSRequestProfile : IValidEntity, ILPSRequestEntity
    {
        protected ILPSLogger _logger;
        protected ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        protected ILPSWatchdog _watchdog;
        protected LPSRequestProfile()
        {
            Id = Guid.NewGuid();
        }

        public LPSRequestProfile(LPSRequestProfile.SetupCommand command, ILPSLogger logger,
            ILPSWatchdog watchdog,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Id = Guid.NewGuid();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
        }
        public Guid Id { get; protected set; }

        public bool IsValid { get; protected set; }

        public bool HasFailed { get; protected set; }
    }

}
