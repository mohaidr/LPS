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
    public partial class LPSTestCase : IValidEntity, IDomainEntity
    {
        protected ILPSLogger _logger;
        protected IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        protected ILPSWatchdog _watchdog;
        protected LPSTestCase()
        {
            Id = Guid.NewGuid();
        }

        public LPSTestCase(SetupCommand command, ILPSLogger logger, 
            ILPSWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Id = Guid.NewGuid();
            _logger = logger;            
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
            this.Setup(command);
        }

        public Guid Id { get; protected set; }
        public string Name { get; protected set; }
        public bool IsValid { get; protected set; }
    }
}
