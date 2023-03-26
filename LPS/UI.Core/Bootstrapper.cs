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
using Microsoft.Extensions.Logging;
using System.IO;

namespace LPS.UI.Core
{
    internal class Bootstrapper : IBootStrapper
    {
        ICustomLogger _logger;
        IConfiguration _config;
        string[] _args;
        public Bootstrapper(ICustomLogger logger, IConfiguration config, dynamic cmdArgs)
        {
            _logger = logger;
            _config = config;
            _args = cmdArgs.args;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("===================");
            Console.WriteLine("LPS Testing Tool V1");
            Console.WriteLine("===================");

            await _logger?.LogAsync("0000-0000-0000", "-------------- LPS V1 - App Started --------------", LoggingLevel.INF);

            LPSTest.SetupCommand lpsTestCommand = new LPSTest.SetupCommand();

            if (_args != null && _args.Length > 0)
            {
                var commandLineParser = new CommandLineParser(_logger, lpsTestCommand);
                commandLineParser.CommandLineArgs = _args;
                commandLineParser.Parse();
                Console.WriteLine("====================================================");
                Console.WriteLine("Command Successfully Executed - Ctrl+C To Exit");
                Console.WriteLine("====================================================");
            }
            else
            {
                var manualBuild = new ManualBuild(new LPSTestValidator(lpsTestCommand));
                manualBuild.Build(lpsTestCommand);
                await new LpsRunner().Run(lpsTestCommand, _logger);
                File.WriteAllText($"{lpsTestCommand.Name}.json", new LpsSerializer().Serialize(lpsTestCommand));
                string action;
                while (true)
                {
                    Console.WriteLine("Press any key to exit, enter \"start over\" to start over or \"redo\" to repeat the same test ");
                    action = Console.ReadLine()?.Trim().ToLower();
                    if (action == "redo")
                    {
                        var lpsTest = new LPSTest(lpsTestCommand, _logger);
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
            }

            await _logger.Flush();
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("App Closed");
            await _logger?.LogAsync("0000-0000-0000", "--------------  LPS V1 - App Closed  --------------", LoggingLevel.INF);
            await _logger?.Flush();
        }
    }
}
