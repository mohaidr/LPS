using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{
    public enum LPSRunType
    { 
        HttpRun,
        WebSocketRun
    }
    //This should be a Non-Entity Superclass
    public partial class Run : IValidEntity, IDomainEntity
    {
        protected ILogger _logger;
        protected IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        protected IWatchdog _watchdog;
        protected IMetricsDataMonitor _lpsMonitoringEnroller;
        protected CancellationTokenSource _cts;
        protected Run()
        {
            Id = Guid.NewGuid();
        }

        public Run(SetupCommand command, ILogger logger, 
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            Id = Guid.NewGuid();
            _logger = logger;            
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            this.Setup(command);
        }

        public Guid Id { get; protected set; }
        public string Name { get; protected set; }
        public bool IsValid { get; protected set; }

        public LPSRunType Type { get; protected set; }
    }
}
