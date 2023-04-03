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

    public partial class LPSTestCase
    {
        public class ExecuteCommand : IAsyncCommand<LPSTestCase>
        {
            private readonly ProtectedAccessTestExecuteCommand protectedAccessTestExecuteCommand = new ProtectedAccessTestExecuteCommand();
            private class ProtectedAccessTestExecuteCommand : LPSTestPlan.ExecuteCommand
            {
                public new int SafelyIncrementNumberofSentRequests(LPSTestPlan.ExecuteCommand dto)
                {
                    return base.SafelyIncrementNumberofSentRequests(dto);
                }
            }
            public LPSTestPlan.ExecuteCommand LPSTestPlanExecuteCommand { get; set; }

            public ExecuteCommand()
            {
                LPSTestPlanExecuteCommand = new LPSTestPlan.ExecuteCommand();
            }

            async public Task ExecuteAsync(LPSTestCase entity)
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
                protectedAccessTestExecuteCommand.SafelyIncrementNumberofSentRequests(dto.LPSTestPlanExecuteCommand);
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
                if (this.Mode == IterationMode.R)
                {

                    Task[] awaitableTasks = new Task[this.Count.Value];

                    Console.WriteLine($"{this.Count} Async call(s) are being sent to {this.LPSRequest.URL}");
                    LPSRequest.ExecuteCommand command = new LPSRequest.ExecuteCommand() { LPSTestCaseExecuteCommand = dto };
                    for (int i = 0; i < this.Count; i++)
                    {
                        awaitableTasks[i] = command.ExecuteAsync(LPSRequest);
                    }

                    _ = ReportAsync(dto, this.LPSRequest.URL, this.Count.Value);
                    await Task.WhenAll(awaitableTasks);

                    Console.WriteLine($"All requests has been processed by {this.LPSRequest.URL} with {dto.NumberOfSuccessfullyCompletedRequests} successfully completed requests and {dto.NumberOfFailedToCompleteRequests} failed to complete requests");
                }
                else
                {
                    throw new NotImplementedException("No implementation yet");
                }
            }
        }
        private async Task ReportAsync(ExecuteCommand dto, string url, int requestCount)
        {
            while ((dto.NumberOfSuccessfullyCompletedRequests + dto.NumberOfFailedToCompleteRequests) != requestCount)
            {
                Console.WriteLine($"    Host: {url}, Successfully completed: {dto.NumberOfSuccessfullyCompletedRequests}, Faile to complete:{dto.NumberOfFailedToCompleteRequests}");
                await Task.Delay(5000);
            }
        }
    }
}
