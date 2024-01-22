using FluentValidation;
using FluentValidation.Results;
using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
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
            _addCommand = new Command("add", "Add an http run");

            LPSCommandLineOptions.AddOptionsToCommand(_addCommand, typeof(LPSCommandLineOptions.LPSAddCommandOptions));
            _rootLpsCliCommand.AddCommand(_addCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _addCommand.SetHandler((testName, lpsRun) =>
            {
                bool isValidPlan, isValidRun, isValidRequestProfile;
                ValidationResult results;
                _planSetupCommand = LPSSerializationHelper.Deserialize<LPSTestPlan.SetupCommand>(File.ReadAllText($"{testName}.json"));
                var planValidator = new LPSTestPlanValidator(_planSetupCommand);
                results = planValidator.Validate();
                isValidPlan = results.IsValid;
                if (!isValidPlan)
                {
                    results.PrintValidationErrors();
                }
                var lpsRunValidator = new LPSRunValidator(lpsRun);
                results = lpsRunValidator.Validate();
                isValidRun = results.IsValid;
                if (!isValidRun)
                {
                    results.PrintValidationErrors();
                }
                var lpsRequestProfileValidator = new LPSRequestProfileValidator(lpsRun.LPSRequestProfile);
                results = lpsRequestProfileValidator.Validate();
                isValidRequestProfile = results.IsValid;
                if (!isValidRequestProfile)
                {
                    results.PrintValidationErrors();
                }

                if (isValidRun && isValidPlan && isValidRequestProfile)
                {
                    _planSetupCommand.LPSHttpRuns.Add(lpsRun);
                    _planSetupCommand.IsValid = true;
                    string json = LPSSerializationHelper.Serialize(_planSetupCommand);
                    File.WriteAllText($"{testName}.json", json);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Your run has been added successfully");
                    Console.ResetColor();
                }
            },
            LPSCommandLineOptions.LPSAddCommandOptions.TestNameOption,
            new LPSRunCommandBinder());
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
