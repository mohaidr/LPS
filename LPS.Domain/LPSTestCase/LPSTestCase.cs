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

    public partial class LPSTestCase : IValidEntity, IExecutable
    {
        private ILPSLogger _logger;

        protected LPSTestCase()
        {
        }

        public LPSTestCase(SetupCommand command, ILPSLogger logger)
        {
            _logger = logger;
            this.Setup(command);
        }

        public Guid Id { get; protected set; }
        public string Name { get; protected set; }
        public bool IsValid { get; protected set; }


    }
}
