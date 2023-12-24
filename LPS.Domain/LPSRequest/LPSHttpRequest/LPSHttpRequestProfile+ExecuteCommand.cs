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
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSHttpRequestProfile
    {
        private class ProtectedAccessLPSTestCaseExecuteCommand : LPSHttpTestCase.ExecuteCommand
        {
            public new int SafelyIncrementNumberofSentRequests(LPSHttpTestCase.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberofSentRequests(command);
            }
            public new int SafelyIncrementNumberOfFailedRequests(LPSHttpTestCase.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfFailedRequests(command);
            }
            public new int SafelyIncrementNumberOfSuccessfulRequests(LPSHttpTestCase.ExecuteCommand command)
            {
                return base.SafelyIncrementNumberOfSuccessfulRequests(command);
            }
        }

        public class ExecuteCommand : IAsyncCommand<LPSHttpRequestProfile> 
        {
            private ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> _httpClientService { get; set; }

            public ExecuteCommand(ILPSClientService<LPSHttpRequestProfile, LPSHttpResponse> httpClientService, LPSHttpTestCase.ExecuteCommand caseExecCommand)
            {
                _httpClientService = httpClientService;
                LPSTestCaseExecuteCommand = caseExecCommand;
            }

            public LPSHttpTestCase.ExecuteCommand LPSTestCaseExecuteCommand { get; set; }

            async public Task ExecuteAsync(LPSHttpRequestProfile entity, ICancellationTokenWrapper cancellationTokenWrapper)
            {
                entity._httpClientService = this._httpClientService;
                await entity.ExecuteAsync(this, cancellationTokenWrapper);
            }
        }


        async private Task ExecuteAsync(ExecuteCommand command, ICancellationTokenWrapper cancellationTokenWrapper)
        {
            if (this.IsValid)
            {
                string hostName = new Uri(this.URL).Host;
                await _watchdog.Balance(hostName);
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
                    
                    int sequenceNumber = _protectedCommand.SafelyIncrementNumberofSentRequests(command.LPSTestCaseExecuteCommand);
                    ((LPSHttpRequestProfile)clonedEntity).LastSequenceId = sequenceNumber;
                    this.LastSequenceId = sequenceNumber;
                    await _httpClientService.SendAsync(((LPSHttpRequestProfile)clonedEntity), cancellationTokenWrapper);
                    this.HasFailed = false;
                    _protectedCommand.SafelyIncrementNumberOfSuccessfulRequests(command.LPSTestCaseExecuteCommand);
                }
                catch
                {
                    _protectedCommand.SafelyIncrementNumberOfFailedRequests(command.LPSTestCaseExecuteCommand);
                    this.HasFailed = true;
                }
            }
        }
    }
}
