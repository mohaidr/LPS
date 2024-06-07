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
using LPS.Domain.Common.Interfaces;

namespace LPS.Domain
{

    public partial class LPSHttpRequestProfile
    {
        private class ProtectedAccessLPSRunExecuteCommand : LPSHttpRun.ExecuteCommand
        {
            public new int SafelyIncrementNumberofSentRequests(LPSHttpRun.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberofSentRequests(command);
            }
            public new int SafelyIncrementNumberOfFailedRequests(LPSHttpRun.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfFailedRequests(command);
            }
            public new int SafelyIncrementNumberOfSuccessfulRequests(LPSHttpRun.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfSuccessfulRequests(command);
            }
        }

        public class ExecuteCommand : IAsyncCommand<LPSHttpRequestProfile> 
        {
            private ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> _httpClientService { get; set; }
            ILPSLogger _logger;
            ILPSWatchdog _watchdog;
            ILPSRuntimeOperationIdProvider _runtimeOperationIdProvider;

            public ExecuteCommand(ILPSClientService<LPSHttpRequestProfile,
                LPSHttpResponse> httpClientService,
                LPSHttpRun.ExecuteCommand caseExecCommand, 
                ILPSLogger logger,
                ILPSWatchdog watchdog,
                ILPSRuntimeOperationIdProvider runtimeOperationIdProvider)
            {
                _httpClientService = httpClientService;
                LPSRunExecuteCommand = caseExecCommand;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
            }
            private AsyncCommandStatus _executionStatus;
            public AsyncCommandStatus Status => _executionStatus;
            public LPSHttpRun.ExecuteCommand LPSRunExecuteCommand { get; set; }

            async public Task ExecuteAsync(LPSHttpRequestProfile entity, ICancellationTokenWrapper cancellationTokenWrapper)
            {
                if (entity == null)
                {
                    _logger.Log(_runtimeOperationIdProvider.OperationId, "LPSHttpRequestProfile Entity Must Have a Value", LPSLoggingLevel.Error);
                    throw new ArgumentNullException(nameof(entity));
                }
                entity._httpClientService = this._httpClientService;
                entity._logger = this._logger;
                entity._watchdog = this._watchdog;
                entity._runtimeOperationIdProvider = this._runtimeOperationIdProvider;
                await entity.ExecuteAsync(this, cancellationTokenWrapper);
            }
        }   


        async private Task ExecuteAsync(ExecuteCommand command, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            if (this.IsValid)
            {
                string hostName = new Uri(this.URL).Host;

                await _watchdog.BalanceAsync(hostName, cancellationTokenWrapper);
                /* 
                 * Clone the entity so we send a different entity to the http client service.
                 * To avoid writing to the same instnace concurrently where we update the sequence number which is used by the http client service, so if the sequence number changes while used by the http client service, then a wrong sequence number will be used and may result in exceptions or unexpected behaviors
                 * This logic may change in the future when we refactor the http client service
                */
                var clonedEntity = this.Clone();
                try
                {
                    if (this._httpClientService == null)
                    {
                        throw new InvalidOperationException("Http Client Is Not Defined");
                    }
                    
                    int sequenceNumber = _protectedCommand.SafelyIncrementNumberofSentRequests(command.LPSRunExecuteCommand);
                    ((LPSHttpRequestProfile)clonedEntity).LastSequenceId = sequenceNumber;
                    this.LastSequenceId = sequenceNumber;
                    await _httpClientService.SendAsync(((LPSHttpRequestProfile)clonedEntity), cancellationTokenWrapper);
                    this.HasFailed = false;
                    _protectedCommand.SafelyIncrementNumberOfSuccessfulRequests(command.LPSRunExecuteCommand);
                }
                catch
                {
                    _protectedCommand.SafelyIncrementNumberOfFailedRequests(command.LPSRunExecuteCommand);
                    this.HasFailed = true;
                    throw;
                }
            }
        }
    }
}
