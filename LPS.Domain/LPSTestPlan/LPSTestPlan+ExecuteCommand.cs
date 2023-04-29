using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTestPlan
    {
        public class ExecuteCommand : IAsyncCommand<LPSTestPlan>
        {
            async public Task ExecuteAsync(LPSTestPlan entity, CancellationToken cancellationToken)
            {
                Reset();
                await entity.ExecuteAsync(this, cancellationToken);
            }

            private void Reset()
            {
                _numberofSentRequests = 0;
            }

            private int _numberofSentRequests;
            public int NumberOfSentRequests { get { return _numberofSentRequests; } }
            protected int SafelyIncrementNumberofSentRequests(ExecuteCommand dto)
            {
                return Interlocked.Increment(ref dto._numberofSentRequests);
            }
        }
        async private Task ExecuteAsync(ExecuteCommand command, CancellationToken cancellationToken)
        {
            if (this.IsValid)
            {
                List<Task> awaitableTasks = new List<Task>();

                for (int i = 0; i < this.NumberOfClients; i++)
                {
                    foreach (var testCase in this.LPSTestCases)
                    {
                        LPSHttpTestCase.ExecuteCommand testCaseExecutecommand = new LPSHttpTestCase.ExecuteCommand() { LPSTestPlanExecuteCommand = command };
                        awaitableTasks.Add(testCaseExecutecommand.ExecuteAsync(testCase, cancellationToken));
                    }

                    if (this.RampUpPeriod>0)
                    { 
                        await Task.Delay(this.RampUpPeriod);
                    }
                }
                await Task.WhenAll(awaitableTasks.ToArray());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("...We are done :)...");
                Console.ResetColor();
            }
        }
    }
}

