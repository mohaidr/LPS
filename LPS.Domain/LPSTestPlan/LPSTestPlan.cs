using System;
using System.Collections.Generic;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTestPlan : IAggregateRoot, IValidEntity, IExecutable
    {

        private ICustomLogger _logger;
        private LPSTestPlan()
        {

        }
        public LPSTestPlan(SetupCommand dto, ICustomLogger logger)
        {
            LPSTestCases = new List<LPSTestCase>();
            _logger = logger;
            this.Setup(dto);
        }

        public IList<LPSTestCase> LPSTestCases { get; private set; }
        public string Name { set; private get; }

        public bool IsRedo { get; private set; }

        public bool IsValid { get; private set; }
    }
}
