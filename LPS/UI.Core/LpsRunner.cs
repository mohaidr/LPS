using LPS.Domain;
using LPS.Domain.Common;
using System;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class LpsRunner
    {
        public async Task Run(LPSTestPlan.SetupCommand planCommand, ICustomLogger logger)
        {
            if (planCommand.IsValid)
            {
                await logger.LogAsync("0000-0000-0000", "New Test Has Been Started", LoggingLevel.INF);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("...Test has started...");
                Console.ResetColor();

                var lpsTest = new LPSTestPlan(planCommand, logger);
                await new LPSTestPlan.ExecuteCommand().ExecuteAsync(lpsTest);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("...Test has completed...");
                Console.ResetColor();
                await logger.LogAsync("0000-0000-0000", "Test Has Completed", LoggingLevel.INF);
            }
        }
    }
}
