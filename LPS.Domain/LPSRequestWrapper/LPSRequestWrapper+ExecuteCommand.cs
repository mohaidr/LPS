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

            private int _numberOfContainerSuccessfullyCompletedRequests;
            private int _numberOfContainerFailedToCompleteRequests;
            private int _numberOfContianerSentRequests;

            protected int SafelyIncrementSuccessfulCallsCounter(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberOfContainerSuccessfullyCompletedRequests);
            }

            protected int SafelyIncrementFailedCallsCounter(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberOfContainerFailedToCompleteRequests);
            }

            protected int SafelyIncrementNumberofContainerSentRequests(ExecuteCommand dto)
            {
                protectedAccessTestExecuteCommand.SafelyIncrementNumberofSentRequests(dto.LPSTestExecuteCommand);
                return Interlocked.Increment(ref dto._numberOfContianerSentRequests);
            }

            public int NumberOfContainerSentRequests { get { return _numberOfContianerSentRequests; } }
            public int NumberOfContainerSuccessfullyCompletedRequests { get { return _numberOfContainerSuccessfullyCompletedRequests; } }
            public int NumberOfContainerFailedToCompleteRequests { get { return _numberOfContainerFailedToCompleteRequests; } }
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

                Console.WriteLine($"All requests has been processed by {this.LPSRequest.URL} with {dto.NumberOfContainerSuccessfullyCompletedRequests} successfully completed requests and {dto.NumberOfContainerFailedToCompleteRequests} failed to complete requests");
            }
        }
        private async Task ReportAsync(ExecuteCommand dto, string url, int numberOAsyncfRepeats)
        {
            while ((dto.NumberOfContainerSuccessfullyCompletedRequests + dto.NumberOfContainerFailedToCompleteRequests) != numberOAsyncfRepeats)
            {
                Console.WriteLine($"    Host: {url}, Successfully completed: {dto.NumberOfContainerSuccessfullyCompletedRequests}, Faile to complete:{dto.NumberOfContainerFailedToCompleteRequests}");
                await Task.Delay(5000);
            }
        }
    }
}
