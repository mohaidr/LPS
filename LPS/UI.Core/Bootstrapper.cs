using LPS.UI.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LPS.UI.Core.UI.Build.Services;
using LPS.Domain;
using Microsoft.Extensions.Hosting;
using LPS.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace LPS.UI.Core
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
            LPSTest.SetupCommand lpsTestCommand = new LPSTest.SetupCommand();

            if (_args != null && _args.Length > 0)
            {
                var commandLineParser = new CommandLineParser();
                commandLineParser.CommandLineArgs = _args;
                commandLineParser.Parse(lpsTestCommand);
            }
            else
            {

                var manualBuild = new ManualBuild(new LPSTestValidator(lpsTestCommand));
                manualBuild.Build(lpsTestCommand);
            }

            await _logger.LogAsync("0000-0000-0000", "New Test Has Been Started", LoggingLevel.INF);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has started...");
            Console.ResetColor();

            var lpsTest = new LPSTest(lpsTestCommand, _logger);
            await new LPSTest.ExecuteCommand().ExecuteAsync(lpsTest);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("...Test has completed...");
            Console.ResetColor();
            string action;
            while (true)
            {
                Console.WriteLine("Press any key to exit, enter \"start over\" to start over or \"redo\" to repeat the same test ");
                action = Console.ReadLine()?.Trim().ToLower();
                if (action == "redo")
                {
                    await new LPSTest.RedoCommand().ExecuteAsync(lpsTest);
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
            Console.WriteLine("Shitting Down...");
            if (_logger != null)
            {
                await _logger.LogAsync("0000-0000-0000", "Exited...", LoggingLevel.INF);
                await _logger.Flush();
            }
        }
    }
}
