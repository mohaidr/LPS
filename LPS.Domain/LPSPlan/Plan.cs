using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Interfaces;

namespace LPS.Domain
{
    //TODO: Refactor this to base and subclasses 
    public partial class Plan : IAggregateRoot, IValidEntity, IDomainEntity, IBusinessEntity
    {

        private ILogger _logger;
        private Plan()
        {
            Rounds = new List<Round>();
        }

        IClientManager<HttpSession, HttpResponse, IClientService<HttpSession, HttpResponse>> _lpsClientManager;
        IClientConfiguration<HttpSession> _lpsClientConfig;
        IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        IWatchdog _watchdog;
        IMetricsDataMonitor _lpsMetricsDataMonitor;
        ICommandStatusMonitor<IAsyncCommand<HttpIteration>, HttpIteration> _httpIterationExecutionCommandStatusMonitor;
        CancellationTokenSource _cts;
        public Plan(SetupCommand command,
            ILogger logger,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            ArgumentNullException.ThrowIfNull(command);
            Rounds = new List<Round>();
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _logger = logger;
            Id = Guid.NewGuid();
            this.Setup(command);
        }
        public Guid Id { get; protected set; }
        public string Name { get; private set; }
        public bool IsValid { get; private set; }
        private ICollection<Round> Rounds { get; set; }
    }
}
