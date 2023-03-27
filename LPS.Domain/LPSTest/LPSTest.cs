using System;
using System.Collections.Generic;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTest : IAggregateRoot, IValidEntity, IExecutable
    {
        //TODO: Implement Mode Based Test
        public enum TestMode
        {
            Load,
            Performence,
            Stress
        }

        private ICustomLogger _logger;
        private LPSTest()
        {

        }
        public LPSTest(SetupCommand dto, ICustomLogger logger)
        {
            LPSRequestWrappers = new List<LPSRequestWrapper>();
            _logger = logger;
            this.Setup(dto);
        }

        public IList<LPSRequestWrapper> LPSRequestWrappers { get; private set; }
        public TestMode Mode { set; private get; }
        public string Name { set; private get; }

        public bool IsRedo { get; private set; }

        public bool IsValid { get; private set; }
    }
}
