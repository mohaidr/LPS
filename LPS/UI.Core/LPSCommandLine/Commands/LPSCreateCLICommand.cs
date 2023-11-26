using FluentValidation.Results;
using LPS.Domain;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Common.Helpers;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.UI.Build.Services;
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
    internal class LPSCreateCLICommand: ILPSCLICommand
    {
        private Command _rootLpsCliCommand;
        private LPSTestPlan.SetupCommand _planSetupCommand;
        private string[] _args;
        private Command _createCommand;
        internal LPSCreateCLICommand(Command rootLpsCliCommand, LPSTestPlan.SetupCommand planSetupCommand, string[] args)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _planSetupCommand = planSetupCommand;
            _args = args;
            Setup();
        }

        private void Setup()
        {
            _createCommand = new Command("create", "Create a new test") {
                LPSCommandLineOptions.TestNameOption,
                LPSCommandLineOptions.NumberOfClientsOption,
                LPSCommandLineOptions.RampupPeriodOption,
                LPSCommandLineOptions.DelayClientCreation,
                LPSCommandLineOptions.RunInParaller
            };
            _rootLpsCliCommand.AddCommand(_createCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _createCommand.SetHandler((lpaTestPlan) =>
            {
                bool isValidPlan;
                ValidationResult results;
                var planValidator = new LPSTestPlanValidator(lpaTestPlan);
                results = planValidator.Validate();
                isValidPlan = results.IsValid;
                if (!isValidPlan)
                {
                    results.PrintValidationErrors();
                }
                else
                {
                    _planSetupCommand.Name = lpaTestPlan.Name;
                    _planSetupCommand.NumberOfClients = lpaTestPlan.NumberOfClients;
                    _planSetupCommand.DelayClientCreationUntilIsNeeded = lpaTestPlan.DelayClientCreationUntilIsNeeded;
                    _planSetupCommand.RampUpPeriod = lpaTestPlan.RampUpPeriod;
                    _planSetupCommand.RunInParallel = lpaTestPlan.RunInParallel;
                    string json = LPSSerializationHelper.Serialize(_planSetupCommand);
                    File.WriteAllText($"{lpaTestPlan.Name}.json", json);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Your test plan has been created successfully");
                    Console.ResetColor();
                }
            },
            new LPSTestPlanCommandBinder(
                LPSCommandLineOptions.TestNameOption,
                LPSCommandLineOptions.NumberOfClientsOption,
                LPSCommandLineOptions.RampupPeriodOption,
                LPSCommandLineOptions.DelayClientCreation,
                LPSCommandLineOptions.RunInParaller));
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
