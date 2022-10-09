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
using AsyncTest.Domain.Common;

namespace AsyncTest.Domain
{

    public partial class HttpAsyncRequestContainer
    {

        public class ExecuteCommand: ICommand<HttpAsyncRequestContainer>
        {
            private readonly ProtectedAccessTestExecuteCommand protectedAccessTestExecuteCommand = new ProtectedAccessTestExecuteCommand();
            private class ProtectedAccessTestExecuteCommand : HttpAsyncTest.ExecuteCommand
            {
                public new int SafelyIncrementNumberofSentRequests(HttpAsyncTest.ExecuteCommand dto)
                {
                   return base.SafelyIncrementNumberofSentRequests(dto);
                }
            }
            public HttpAsyncTest.ExecuteCommand HttpAsyncTestExecuteCommand { get; set; }

            public ExecuteCommand()
            {
                HttpAsyncTestExecuteCommand = new HttpAsyncTest.ExecuteCommand();
            }
            public void Execute(HttpAsyncRequestContainer entity)
            {
                throw new NotImplementedException();
            }

            async public Task ExecuteAsync(HttpAsyncRequestContainer entity)
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
                protectedAccessTestExecuteCommand.SafelyIncrementNumberofSentRequests(dto.HttpAsyncTestExecuteCommand);
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

                Console.WriteLine($"{this.NumberofAsyncRepeats} Async call(s) are bing sent to {this.HttpRequest.URL}");
                HttpAsyncRequest.ExecuteCommand command = new HttpAsyncRequest.ExecuteCommand() { HttpAsyncRequestContainerExecuteCommand = dto };
                for (int i = 0; i < this.NumberofAsyncRepeats; i++)
                {
                    awaitableTasks[i] = command.ExecuteAsync(HttpRequest);
                }

                _= ReportAsync(dto, this.HttpRequest.URL, this.NumberofAsyncRepeats);
                await Task.WhenAll(awaitableTasks);

                Console.WriteLine($"All requests has been processed by {this.HttpRequest.URL} with {dto.NumberOfContainerSuccessfullyCompletedRequests} successfully completed requests and {dto.NumberOfContainerFailedToCompleteRequests} failed to complete requests");
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
