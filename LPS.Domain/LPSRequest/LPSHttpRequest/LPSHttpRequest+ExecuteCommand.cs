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

    public partial class LPSHttpRequest
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

        new public class ExecuteCommand : IAsyncCommand<LPSHttpRequest> 
        {
            internal ILPSClientService<LPSHttpRequest> HttpClientService { get; set; }

            public ExecuteCommand()
            {
                LPSTestCaseExecuteCommand = new LPSHttpTestCase.ExecuteCommand();
            }

            public LPSHttpTestCase.ExecuteCommand LPSTestCaseExecuteCommand { get; set; }

            async public Task ExecuteAsync(LPSHttpRequest entity, CancellationToken cancellationToken)
            {
                await entity.ExecuteAsync(this, cancellationToken);
            }
        }

        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken cancellationToken)
        {
            if (this.IsValid)
            {
                int requestNumber;
                ProtectedAccessLPSTestCaseExecuteCommand protectedCommand = new ProtectedAccessLPSTestCaseExecuteCommand();
                try
                {
                    if (command.HttpClientService == null)
                    {
                        throw new InvalidOperationException("Http Client Is Not Defined");
                    }

                    requestNumber = protectedCommand.SafelyIncrementNumberofSentRequests(command.LPSTestCaseExecuteCommand);
                    var clientServiceTask = command.HttpClientService.Send(this, requestNumber.ToString(), cancellationToken);
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
