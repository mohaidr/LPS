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

    public partial class LPSRequestWrapper
    {

        public class ExecuteCommand: IAsyncCommand<LPSRequestWrapper>
        {
            private readonly ProtectedAccessTestExecuteCommand protectedAccessTestExecuteCommand = new ProtectedAccessTestExecuteCommand();
            private class ProtectedAccessTestExecuteCommand : LPSTest.ExecuteCommand
            {
                public new int SafelyIncrementNumberofSentRequests(LPSTest.ExecuteCommand dto)
                {
                   return base.SafelyIncrementNumberofSentRequests(dto);
                }
            }
            public LPSTest.ExecuteCommand LPSTestExecuteCommand { get; set; }

            public ExecuteCommand()
            {
                LPSTestExecuteCommand = new LPSTest.ExecuteCommand();
            }

            async public Task ExecuteAsync(LPSRequestWrapper entity)
            {
                await entity.ExecuteAsync(this);
            }

            private int _numberOfSuccessfullyCompletedRequests;
            private int _numberOfFailedToCompleteRequests;
            private int _numberOfSentRequests;

            protected int SafelyIncrementNumberOfSuccessfulRequests(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberOfSuccessfullyCompletedRequests);
            }

            protected int SafelyIncrementNumberOfFaildRequests(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberOfFailedToCompleteRequests);
            }

            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand dto)
            {
                protectedAccessTestExecuteCommand.SafelyIncrementNumberofSentRequests(dto.LPSTestExecuteCommand);
                return Interlocked.Increment(ref dto._numberOfSentRequests);
            }

            public int NumberOfSentRequests { get { return _numberOfSentRequests; } }
            public int NumberOfSuccessfullyCompletedRequests { get { return _numberOfSuccessfullyCompletedRequests; } }
            public int NumberOfFailedToCompleteRequests { get { return _numberOfFailedToCompleteRequests; } }
        }
        async private Task ExecuteAsync(ExecuteCommand dto)
        {
            if (this.IsValid)
            {
                Task[] awaitableTasks = new Task[this.NumberofAsyncRepeats];

                Console.WriteLine($"{this.NumberofAsyncRepeats} Async call(s) are bing sent to {this.LPSRequest.URL}");
                LPSRequest.ExecuteCommand command = new LPSRequest.ExecuteCommand() { LPSRequestWrapperExecuteCommand = dto };
                for (int i = 0; i < this.NumberofAsyncRepeats; i++)
                {
                    awaitableTasks[i] = command.ExecuteAsync(LPSRequest);
                }

                _= ReportAsync(dto, this.LPSRequest.URL, this.NumberofAsyncRepeats);
                await Task.WhenAll(awaitableTasks);

                Console.WriteLine($"All requests has been processed by {this.LPSRequest.URL} with {dto.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {dto.NumberOfFailedToCompleteRequests} failed to complete requests");
            }
        }
        private async Task ReportAsync(ExecuteCommand dto, string url, int numberOAsyncfRepeats)
        {
            while ((dto.NumberOfSuccessfullyCompletedRequests + dto.NumberOfFailedToCompleteRequests) != numberOAsyncfRepeats)
            {
                Console.WriteLine($"    Host: {url}, Successfully completed: {dto.NumberOfSuccessfullyCompletedRequests}, Faile to complete:{dto.NumberOfFailedToCompleteRequests}");
                await Task.Delay(5000);
            }
        }
    }
}
