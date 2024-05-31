using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static LPS.Domain.LPSHttpRun;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class RedoCommand : IAsyncCommand<LPSTestPlan>
        {
            private AsyncCommandStatus _executionStatus;
            public AsyncCommandStatus Status => _executionStatus;
            async public Task ExecuteAsync(LPSTestPlan entity, ICancellationTokenWrapper cancellationTokenWrapper)
            {
                await entity.RedoAsync(this, cancellationTokenWrapper);
            }
        }

        async private Task RedoAsync(RedoCommand command, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            if (this.IsValid)
            {
                this.IsRedo = true;
                await this.ExecuteAsync(new ExecuteCommand(_logger, _watchdog, _runtimeOperationIdProvider, _lpsClientManager, _lpsClientConfig, _httpRunExecutionCommandStatusMonitor, _lpsDataMetricsMonitor), cancellationTokenWrapper);
            }
        }
    }
}

