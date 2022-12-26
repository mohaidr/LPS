using AsyncCalls.UI.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncCalls.UI.Core.UI.Build.Services;
using AsyncTest.Domain;
using Microsoft.Extensions.Hosting;
using AsyncTest.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace AsyncCalls.UI.Core
{
    internal class Bootstrapper : IBootStrapper
    {
        IFileLogger _logger;
        IConfiguration _config;
        string[] _args;
        public Bootstrapper(IFileLogger logger, IConfiguration config, dynamic cmdArgs)
        {
            _logger = logger;
            _config = config;
            _args = cmdArgs.args;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            HttpAsyncTest.SetupCommand httpasynsTestCommand = new HttpAsyncTest.SetupCommand();
            if (_args != null && _args.Length > 0)
            {
                var commandLineParser = new CommandLineParser(new HttpAsyncRequestWrapperValidator());
                commandLineParser.CommandLineArgs = _args;
                commandLineParser.Parse(httpasynsTestCommand);
            }
            else
            {

                var manualBuild = new ManualBuild(new HttpAsyncRequestWrapperValidator());
                manualBuild.Build(httpasynsTestCommand);
            }


            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has started...");
            Console.ResetColor();

            var httpAsyncTest = new HttpAsyncTest(httpasynsTestCommand, _logger);
            await new HttpAsyncTest.ExecuteCommand().ExecuteAsync(httpAsyncTest);

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
                    await new HttpAsyncTest.RedoCommand().ExecuteAsync(httpAsyncTest);
                    continue;
                }
                break;
            }
            if (action == "start over")
            {
                _args = new string[] { };
                await StartAsync(new CancellationToken());
            }

            await _logger.Flush();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
