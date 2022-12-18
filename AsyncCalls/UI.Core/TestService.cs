using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncCalls.UI.Common;
using AsyncTest.Domain;
using AsyncTest.Domain.Common;
using AsyncTest.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace AsyncTest.UI.Core
{

    //redesign this class to remove redundent code and unify both manual and command build functions if it makes sense, also remove unnecessary complexity 
    internal class TestService<T1, T2> : ITestService<T1, T2> where T1: ICommand<T2> where T2: IExecutable
    {
        private readonly IFileLogger _logger;
        private readonly IConfiguration _config;
        public TestService(IFileLogger loggger, IConfiguration config)
        {
            _logger = loggger;
            _config = config;
        }

      

        public async Task Run(IBuilderService<T1, T2> buildService, string[] args)
        {
           // buildService.Build();

            var command = new HttpAsyncTest.SetupCommand();
            if (args.Length != 0)
            {
                TestService<T1, T2>.BuildFromCommand(command, args);
            }
            else
            {
                Console.WriteLine("Start building your test manually");
                TestService<T1, T2>.BuildManual(command);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has started...");
            Console.ResetColor();

            HttpAsyncTest asyncTest = new HttpAsyncTest(command, _logger);
            await new HttpAsyncTest.ExecuteCommand().ExecuteAsync(asyncTest);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has completed...");
            Console.ResetColor();
            string action;
            while (true)
            {
                Console.WriteLine("Press any key to exit, enter \"start over\" to start over or \"redo\" to repeat the same test ");
                action = Console.ReadLine().Trim().ToLower();
                if (action == "redo")
                {
                    await new HttpAsyncTest.RedoCommand().ExecuteAsync(asyncTest);
                    continue;
                }
                break;
            }
            if (action == "start over")
            {
                args = new string[] { };
                await Run(buildService, args);
            }

          await _logger.Flush();
        }

    }
}
