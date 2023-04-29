using System;
using System.Collections.Generic;
using LPS.Domain.Common;

namespace LPS.Domain
{
    //TODO: Refactor this to base and subclasses 
    public partial class LPSTestPlan : IAggregateRoot, IValidEntity, IExecutable
    {

        private ICustomLogger _logger;
        private LPSTestPlan()
        {

        }

        ILPSClientManager<LPSHttpRequest,
        ILPSClientService<LPSHttpRequest>> _lpsClientManager;
        ILPSClientConfiguration<LPSHttpRequest> _config;
        public LPSTestPlan(SetupCommand command, ILPSClientManager<LPSHttpRequest,
            ILPSClientService<LPSHttpRequest>> lpsClientManager,
            ILPSClientConfiguration<LPSHttpRequest> config, ICustomLogger logger)
        {
            LPSTestCases = new List<LPSHttpTestCase>();
            _lpsClientManager = lpsClientManager;
            _logger = logger;
            _config = config;
            this.Setup(command);
        }

        public IList<LPSHttpTestCase> LPSTestCases { get; private set; }
        public string Name { set; private get; }

        public bool IsRedo { get; private set; }
        public bool? DelayClientCreationUntilIsNeeded { get; private set; }
        public int ClientTimeout { get; private set; }
        public int PooledConnectionLifetime { get; private set; }
        public int PooledConnectionIdleTimeout { get; private set; }
        public int MaxConnectionsPerServer { get; private set; }
        public bool IsValid { get; private set; }

        public int NumberOfClients { get; private set; }
        public int RampUpPeriod { get; private set; }
    }
}
