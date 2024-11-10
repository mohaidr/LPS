using LPS.Domain.Common.Interfaces;
using LPS.Domain.LPSFlow.LPSHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSFlow
{
    public partial class Session : IDomainEntity, IBusinessEntity, IValidEntity
    {
        protected ILogger _logger;
        protected IRuntimeOperationIdProvider _runtimeOperationIdProvider;
        protected IWatchdog _watchdog;
        protected CancellationTokenSource _cts;
        public Guid Id { get; protected set; }

        public Session(SetupCommand command, ILogger logger,
            IWatchdog watchdog,
            IRuntimeOperationIdProvider runtimeOperationIdProvider)
        {
            ArgumentNullException.ThrowIfNull(command);
            Id = Guid.NewGuid();
            _logger = logger;
            _runtimeOperationIdProvider = runtimeOperationIdProvider;
            _watchdog = watchdog;
        }
        HttpRequestProfile _lpsHttpRequestProfile;
        //This should not be allowed to be set explicitly.
        public HttpRequestProfile LPSHttpRequestProfile
        {
            get
            {
                return _lpsHttpRequestProfile;
            }
            set
            {
                _lpsHttpRequestProfile = value != null && value.IsValid ? value : _lpsHttpRequestProfile;
            }
        }

        public bool IsValid { get; protected set; }

        public CapturHandler Captur { get; set; }
        public StopAfterHandler StopAfter { get; set; }
        public StopIfHandler StopAIf { get; set; }
        public ReadHandler Read { get; set; }
        public PauseHandler Pause { get; set; }
    }
}
