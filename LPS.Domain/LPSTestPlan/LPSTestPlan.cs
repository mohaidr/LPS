using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;

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
        ILPSMetricsDataMonitor _lpsDataMetricsMonitor;
        ICommandStatusMonitor<IAsyncCommand<LPSHttpRun>, LPSHttpRun> _httpRunExecutionCommandStatusMonitor;
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
        // Currently it is open for assignment from outside so the user may easly add too many entities, nulls or even orphan entities
        public ICollection<LPSHttpRun> LPSHttpRuns { get; set; }
        public ICollection<LPSHttpRun> _lPSHttpRuns => LPSHttpRuns.Where(run => run != null && run.IsValid).ToList();
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
