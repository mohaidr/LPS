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
        private ICustomLogger _logger;
        /* D refers to Duration
         * C refers to Cool Down
         * B refers to Batch Size  
         * R refers to Request Count
         */
        public enum IterationMode
        {
            DCB,
            CRB,
            CB,
            DC,
            RC,
            R,
            D
        }

        private LPSTestCase()
        {
        }

        public LPSTestCase(SetupCommand command, ICustomLogger logger)
        {
            _logger = logger;
            this.Setup(command);
        }


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
        public int? RequestCount { get; private set; }

        public string Name { get; private set; }
        public bool IsValid { get; private set; }

        public int? Duration { get; private set; }

        public int? BatchSize { get; private set; }

        public int? CoolDownTime { get; private set; }

        public IterationMode? Mode { get; private set; }
    }
}
