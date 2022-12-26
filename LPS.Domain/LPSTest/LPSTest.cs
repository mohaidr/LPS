using System;
using System.Collections.Generic;
using Serilog;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTest : IAggregateRoot, IValidEntity, IExecutable
    {
        private IFileLogger _logger;
        private LPSTest()
        {

        }
        public LPSTest(SetupCommand dto, IFileLogger logger)
        {
            LPSRequestWrappers = new List<LPSRequestWrapper>();
            _logger = logger;
            this.Setup(dto);
        }

        public IList<LPSRequestWrapper> LPSRequestWrappers { get; private set; }

        public string Name { set; private get; }

        public bool IsRedo { get; private set; }

        public bool IsValid { get; private set; }
    }
}
