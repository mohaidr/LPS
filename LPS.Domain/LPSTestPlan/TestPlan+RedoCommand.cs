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
using static LPS.Domain.HttpRun;
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class TestPlan
    {
        public class RedoCommand : IAsyncCommand<TestPlan>
        {
            private AsyncCommandStatus _executionStatus;
            public AsyncCommandStatus Status => _executionStatus;
            async public Task ExecuteAsync(TestPlan entity)
            {
                await entity.RedoAsync(this);
            }
        }

        async private Task RedoAsync(RedoCommand command)
        {
            if (this.IsValid)
            {
                this.IsRedo = true;
                await this.ExecuteAsync(new ExecuteCommand(_logger, _watchdog, _runtimeOperationIdProvider, _lpsClientManager, _lpsClientConfig, _httpRunExecutionCommandStatusMonitor, _lpsMetricsDataMonitor, _cts));
            }
        }
    }
}

