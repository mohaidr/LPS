using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{
    //TODO: Refactor this to base and subclasses 
    public partial class LPSTestPlan : IAggregateRoot, IValidEntity, IDomainEntity
    {

        private ILPSLogger _logger;
        private LPSTestPlan()
        {

        }

        ILPSClientManager<LPSHttpRequestProfile,LPSHttpResponse, ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse>> _lpsClientManager;
        ILPSClientConfiguration<LPSHttpRequestProfile> _lpsClientConfig;
        ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSWatchdog _watchdog;
        ILPSMonitoringEnroller _lpsMonitoringEnroller;
        public LPSTestPlan(SetupCommand command, 
            ILPSLogger logger,
            ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            LPSHttpRuns = new List<LPSHttpRun>();
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _logger = logger;
            Id = Guid.NewGuid();
            this.Setup(command);
        }

        public Guid Id { get; protected set; }
        //TODO: When implementing repositories and DB think about collections and how they should be treated
        // Should this be mapped to the DB?
        // Currently it is open for assignment from out side so the user can easly add too many entities and can add orphan entities
        public ICollection<LPSHttpRun> LPSHttpRuns { get; set; }
        public string Name { get; private set; }

        public bool IsRedo { get; private set; }
        public bool? DelayClientCreationUntilIsNeeded { get; private set; }
        public bool? RunInParallel { get; private set; }
        //ToDo: Implement CleanUp Cookies
        public bool SameClientForEachTeastCase { get; private set; } = true;
        public bool IsValid { get; private set; }
        public int NumberOfClients { get; private set; }
        public int RampUpPeriod { get; private set; }
    }
}
