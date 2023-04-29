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
        ILPSClientConfiguration<LPSHttpRequest> _config;
        ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> _httpClientManager;
        string[] _args;
        public Bootstrapper(ICustomLogger logger,
            ILPSClientConfiguration<LPSHttpRequest> config,
            ILPSClientManager<LPSHttpRequest, ILPSClientService<LPSHttpRequest>> httpClientManager,
            dynamic cmdArgs)
        {
            _logger = logger;
            _config = config;
            _args = cmdArgs.args;
            _httpClientManager = httpClientManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("===================");
            Console.WriteLine("LPS Testing Tool V1");
            Console.WriteLine("===================");

            await _logger?.LogAsync("0000-0000-0000", "-------------- LPS V1 - App Started --------------", LoggingLevel.INF);

            LPSTestPlan.SetupCommand lpsTestPlanSetupCommand = new LPSTestPlan.SetupCommand();

            if (_args != null && _args.Length > 0)
            {
                var commandLineParser = new CommandLineParser(_logger, _httpClientManager, _config, lpsTestPlanSetupCommand);
                commandLineParser.CommandLineArgs = _args;
                commandLineParser.Parse();
                Console.WriteLine("====================================================");
                Console.WriteLine("Command Successfully Executed - Ctrl+C To Exit");
                Console.WriteLine("====================================================");
            }
            else
            {
                var manualBuild = new ManualBuild(new LPSTestPlanValidator(lpsTestPlanSetupCommand));
                manualBuild.Build(lpsTestPlanSetupCommand);
                var lpsManager = new LpsManager(_logger, _httpClientManager, _config);
                await lpsManager.Run(lpsTestPlanSetupCommand);
                File.WriteAllText($"{lpsTestPlanSetupCommand.Name}.json", new LpsSerializer().Serialize(lpsTestPlanSetupCommand));
                string action;
                while (true)
                {
                    Console.WriteLine("Press any key to exit, enter \"start over\" to start over or \"redo\" to trigger the same test ");
                    action = Console.ReadLine()?.Trim().ToLower();
                    if (action == "redo")
                    {
                        lpsTestPlanSetupCommand.Name = string.Concat(lpsTestPlanSetupCommand.Name, ".Redo");
                        await lpsManager.Run(lpsTestPlanSetupCommand);
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
