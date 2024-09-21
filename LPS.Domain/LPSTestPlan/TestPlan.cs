using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.Domain
{
    //TODO: Refactor this to base and subclasses 
    public partial class TestPlan : IAggregateRoot, IValidEntity, IDomainEntity, IBusinessEntity
    {

        private ILogger _logger;
        private TestPlan()
        {

        }

        IClientManager<HttpRequestProfile,HttpResponse, IClientService<HttpRequestProfile, HttpResponse>> _lpsClientManager;
        IClientConfiguration<HttpRequestProfile> _lpsClientConfig;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IWatchdog _watchdog;
        IMetricsDataMonitor _lpsMetricsDataMonitor;
        ICommandStatusMonitor<IAsyncCommand<HttpRun>, HttpRun> _httpRunExecutionCommandStatusMonitor;
        CancellationTokenSource _cts;
        public TestPlan(SetupCommand command, 
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            LPSRuns = new List<Run>();
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _logger = logger;
            Id = Guid.NewGuid();
            this.Setup(command);
        }

        public Guid Id { get; protected set; }
        //TODO: When implementing repositories and DB think about collections and how they should be treated
        // Should this be mapped to the DB?
        // Currently it is open for assignment from outside so the user may easly add too many entities, nulls or even orphan entities
        public ICollection<Run> LPSRuns { get; set; }
        private IReadOnlyCollection<Run> _lPSRuns => LPSRuns.Where(run => run != null && run.IsValid).ToList();
        public string Name { get; private set; }

        public bool IsRedo { get; private set; }
        public bool? DelayClientCreationUntilIsNeeded { get; private set; }
        public bool? RunInParallel { get; private set; }
        //ToDo: Implement CleanUp Cookies
        public bool SameClientForEachTeastCase { get; private set; } = true;
        public bool IsValid { get; private set; }
        public int NumberOfClients { get; private set; }
        public int ArrivalDelay { get; private set; }
    }
}
