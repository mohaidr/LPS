using LPS.Domain;
using LPS.UI.Common;
using LPS.UI.Core.LPSCommandLine.Bindings;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class LPSAddCLICommand: ILPSCLICommand
    {
        private Command _rootLpsCliCommand;
        private LPSTestPlan.SetupCommand _planSetupCommand;
        private Command _addCommand;
        private string[] _args;
        internal LPSAddCLICommand(Command rootLpsCliCommand, LPSTestPlan.SetupCommand planSetupCommand, string[] args)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _planSetupCommand = planSetupCommand;
            _args = args;
            Setup();
        }

        private void Setup()
        {
            _addCommand = new Command("add", "Add an http request")
            {
                LPSCommandLineOptions.TestNameOption,
                LPSCommandLineOptions.CaseNameOption,
                LPSCommandLineOptions.HttpMethodOption,
                LPSCommandLineOptions.HttpversionOption,
                LPSCommandLineOptions.RequestCountOption,
                LPSCommandLineOptions.IterationModeOption,
                LPSCommandLineOptions.Duratiion,
                LPSCommandLineOptions.CoolDownTime,
                LPSCommandLineOptions.BatchSize,
                LPSCommandLineOptions.UrlOption,
                LPSCommandLineOptions.HeaderOption,
                LPSCommandLineOptions.PayloadOption,
                LPSCommandLineOptions.DownloadHtmlEmbeddedResources,
                LPSCommandLineOptions.SaveResponse
            };
            _rootLpsCliCommand.AddCommand(_addCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _addCommand.SetHandler((testName, lpsTestCase) =>
            {
                var serializer = new LpsSerializer();
                _planSetupCommand = serializer.DeSerialize(File.ReadAllText($"{testName}.json"));
                _planSetupCommand.LPSTestCases.Add(lpsTestCase);
                _planSetupCommand.IsValid = true;
                string json = serializer.Serialize(_planSetupCommand);
                File.WriteAllText($"{testName}.json", json);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Your test case has been added successfully");
                Console.ResetColor();
            },
            LPSCommandLineOptions.TestNameOption,
            new LPSTestCaseCommandBinder(
            LPSCommandLineOptions.CaseNameOption,
            LPSCommandLineOptions.RequestCountOption,
            LPSCommandLineOptions.IterationModeOption,
            LPSCommandLineOptions.Duratiion,
            LPSCommandLineOptions.CoolDownTime,
            LPSCommandLineOptions.BatchSize,
            LPSCommandLineOptions.HttpMethodOption,
            LPSCommandLineOptions.HttpversionOption,
            LPSCommandLineOptions.UrlOption,
            LPSCommandLineOptions.HeaderOption,
            LPSCommandLineOptions.PayloadOption,
            LPSCommandLineOptions.DownloadHtmlEmbeddedResources,
            LPSCommandLineOptions.SaveResponse));
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
