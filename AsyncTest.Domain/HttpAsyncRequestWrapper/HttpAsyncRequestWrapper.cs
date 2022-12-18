using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncRequestWrapper : IValidEntity, IExecutable
    {
        private ICustomLogger _logger;

        private HttpAsyncRequestWrapper()
        {
        }

        public HttpAsyncRequestWrapper(SetupCommand dto, ICustomLogger logger)
        {
            _logger = logger;
            this.Setup(dto);
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

        public HttpAsyncRequest HttpRequest { get; private set; }

        public string Name { get; private set; }
        public bool IsValid { get; private set; }
    }
}
