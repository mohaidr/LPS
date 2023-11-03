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
            private ILPSClientService<LPSHttpRequestProfile> _httpClientService { get; set; }

            public ExecuteCommand(ILPSClientService<LPSHttpRequestProfile> httpClientService, LPSHttpTestCase.ExecuteCommand caseExecCommand)
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
                int requestNumber;
                ProtectedAccessLPSTestCaseExecuteCommand protectedCommand = new ProtectedAccessLPSTestCaseExecuteCommand();
                try
                {
                    if (this._httpClientService == null)
                    {
                        throw new InvalidOperationException("Http Client Is Not Defined");
                    }
                    
                    requestNumber = protectedCommand.SafelyIncrementNumberofSentRequests(command.LPSTestCaseExecuteCommand);
                    var clientServiceTask = _httpClientService.SendAsync(this, requestNumber.ToString(), cancellationTokenWrapper);
                    await clientServiceTask;
                    this.HasFailed = false;
                    protectedCommand.SafelyIncrementNumberOfSuccessfulRequests(command.LPSTestCaseExecuteCommand);
                }
                catch (Exception ex)
                {
                    protectedCommand.SafelyIncrementNumberOfFailedRequests(command.LPSTestCaseExecuteCommand);
                    this.HasFailed = true;
                }
            }
        }
    }
}
