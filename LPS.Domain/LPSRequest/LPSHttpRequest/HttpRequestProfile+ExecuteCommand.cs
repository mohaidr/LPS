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

    public partial class HttpRequestProfile
    {
        private class ProtectedAccessLPSRunExecuteCommand : HttpRun.ExecuteCommand
        {
            public new int SafelyIncrementNumberofSentRequests(HttpRun.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberofSentRequests(command);
            }
            public new int SafelyIncrementNumberOfFailedRequests(HttpRun.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfFailedRequests(command);
            }
            public new int SafelyIncrementNumberOfSuccessfulRequests(HttpRun.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfSuccessfulRequests(command);
            }
        }

        public class ExecuteCommand : IAsyncCommand<HttpRequestProfile> 
        {
            private IClientService<HttpRequestProfile, HttpResponse> _httpClientService { get; set; }
            ILogger _logger;
            IWatchdog _watchdog;
            IRuntimeOperationIdProvider _runtimeOperationIdProvider;
            CancellationTokenSource _cts;
            public ExecuteCommand(IClientService<HttpRequestProfile,
                HttpResponse> httpClientService,
                HttpRun.ExecuteCommand runExecCommand, 
                ILogger logger,
                IWatchdog watchdog,
                IRuntimeOperationIdProvider runtimeOperationIdProvider,
                CancellationTokenSource cts)
            {
                _httpClientService = httpClientService;
                RunExecuteCommand = runExecCommand;
                _logger = logger;
                _watchdog = watchdog;
                _runtimeOperationIdProvider = runtimeOperationIdProvider;
                _cts = cts;
            }
            private AsyncCommandStatus _executionStatus;
            public AsyncCommandStatus Status => _executionStatus;
            public HttpRun.ExecuteCommand RunExecuteCommand { get; set; }

            async public Task ExecuteAsync(HttpRequestProfile entity)
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
                entity._cts = this._cts;
                await entity.ExecuteAsync(this);
            }
        }   


        async private Task ExecuteAsync(ExecuteCommand command)
        {
            if (this.IsValid)
            {
                string hostName = new Uri(this.URL).Host;

                await _watchdog.BalanceAsync(hostName);
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
                    
                    int sequenceNumber = _protectedCommand.SafelyIncrementNumberofSentRequests(command.RunExecuteCommand);
                    ((HttpRequestProfile)clonedEntity).LastSequenceId = sequenceNumber;
                    this.LastSequenceId = sequenceNumber;
                    await _httpClientService.SendAsync(((HttpRequestProfile)clonedEntity));
                    this.HasFailed = false;
                    _protectedCommand.SafelyIncrementNumberOfSuccessfulRequests(command.RunExecuteCommand);
                }
                catch
                {
                    _protectedCommand.SafelyIncrementNumberOfFailedRequests(command.RunExecuteCommand);
                    this.HasFailed = true;
                    //TODO: We removed the "throw" line as it is cuasing the whole test to exit
                    //We need to think about not exiting the whole test when an exception occures here
                    //OR Give option for the client to cancel the http run when the customer starts noticing failures 
                }
            }
        }
    }
}
