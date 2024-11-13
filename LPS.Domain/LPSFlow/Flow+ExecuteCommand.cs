using LPS.Domain.Common.Interfaces;
using LPS.Domain.Domain.Common.Enums;
using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Domain.LPSFlow
{
    public partial class Flow
    {
        private IClientService<HttpSession, HttpResponse> _httpClientService;
        public class ExecuteCommand : IAsyncCommand<Flow>
        {
            private ExecutionStatus _executionStatus;

            public ExecutionStatus Status => _executionStatus;
            IClientService<HttpSession, HttpResponse> _httpClientService;
            ILogger _logger;
            IWatchdog _watchdog;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            IMetricsDataMonitor _lpsMonitoringEnroller;
            CancellationTokenSource _cts;
            protected ExecuteCommand()
            {

            }
            public ExecuteCommand(IClientService<HttpSession,
                HttpResponse> httpClientService,
                Round.ExecuteCommand roundExecCommand,
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                IMetricsDataMonitor lpsMonitoringEnroller,
                CancellationTokenSource cts)
            {
                _httpClientService = httpClientService;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _lpsMonitoringEnroller = lpsMonitoringEnroller;
                _cts = cts;
            }
            public async Task ExecuteAsync(Flow entity)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "Flow Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                await entity.ExecuteAsync(this);
            }
        }

        async public Task ExecuteAsync(ExecuteCommand command)
        {

        }
    }
}
