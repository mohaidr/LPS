using System;
using System.Collections.Generic;
using LPS.Domain.Common;

namespace LPS.Domain
{
    //TODO: Refactor this to base and subclasses 
    public partial class LPSTestPlan : IAggregateRoot, IValidEntity, IDomainEntity
    {

        private ILPSLogger _logger;
        private LPSTestPlan()
        {

        }

        ILPSClientManager<LPSHttpRequest,
        ILPSClientService<LPSHttpRequest>> _lpsClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _lpsClientConfig;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        ILPSResourceTracker _resourceUsageTracker;

        public LPSTestPlan(SetupCommand command, ILPSClientManager<LPSHttpRequest,
            ILPSClientService<LPSHttpRequest>> lpsClientManager,
            ILPSClientConfiguration<LPSHttpRequest> lpsClientConfig, ILPSLogger logger,
            ILPSResourceTracker resourceUsageTracker,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            LPSTestCases = new List<LPSHttpTestCase>();
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _logger = logger;
            _lpsClientManager = lpsClientManager;
            _lpsClientConfig = lpsClientConfig;
            _resourceUsageTracker= resourceUsageTracker;
            Id = Guid.NewGuid();
            this.Setup(command);
        }

        public Guid Id { get; protected set; }
        public IList<LPSHttpTestCase> LPSTestCases { get; private set; }
        public string Name { set; private get; }

        public bool IsRedo { get; private set; }
        public bool? DelayClientCreationUntilIsNeeded { get; private set; }
        public bool? RunInParallel { get; private set; }
        //ToDo: Implement CleanUp Cookies
        public bool SameClientForEachTeastCase { get; private set; } = true;
        public int ClientTimeout { get; private set; }
        public int PooledConnectionLifeTime { get; private set; }
        public int PooledConnectionIdleTimeout { get; private set; }
        public int MaxConnectionsPerServer { get; private set; }
        public bool IsValid { get; private set; }
        public int NumberOfClients { get; private set; }
        public int RampUpPeriod { get; private set; }
    }
}
