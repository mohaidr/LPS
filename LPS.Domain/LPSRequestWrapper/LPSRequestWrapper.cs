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

    public partial class LPSRequestWrapper : IValidEntity, IExecutable
    {
        private ICustomLogger _logger;

        private LPSRequestWrapper()
        {
        }

        public LPSRequestWrapper(SetupCommand command, ICustomLogger logger)
        {
            _logger = logger;
            this.Setup(command);
        }

        //TODO: Implement Mode Based Test
        public enum Mode
        {
            TimeBased,
            NumberBased
        }

        //   private StreamWriter UserRequest { get; set; }

        public int NumberofAsyncRepeats { get; private set; }

        private int _numberOfSuccessfulCalls;
        private int _numberOfFailedCalls;

        public int NumberOfSuccessfulCalls
        {
            get => _numberOfSuccessfulCalls;
            set
            {
                if (this.IsValid)
                {
                    _numberOfSuccessfulCalls = value;
                }
            }
        }
        public int NumberOfFailedCalls
        {
            get => _numberOfFailedCalls;
            set
            {
                if (this.IsValid)
                {
                    _numberOfFailedCalls = value;
                }
            }
        }

        public LPSRequest LPSRequest { get; private set; }

        public string Name { get; private set; }
        public bool IsValid { get; private set; }
    }
}
