using FluentValidation;
using FluentValidation.Results;
using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using LPS.UI.Core.UI.Build.Services;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ValidationResult = FluentValidation.Results.ValidationResult;

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
            _addCommand.SetHandler((testName, lpsRunSetupCommand) =>
            {
                ValidationResult planValidationResults, runValidationResulta, requestProfileValidationResults;
                _planSetupCommand = LPSSerializationHelper.Deserialize<LPSTestPlan.SetupCommand>(File.ReadAllText($"{testName}.json"));
                var planValidator = new LPSTestPlanValidator(_planSetupCommand);
                planValidationResults = planValidator.Validate();
                var lpsRunValidator = new LPSRunValidator(lpsRunSetupCommand);
                runValidationResulta = lpsRunValidator.Validate();
                var lpsRequestProfileValidator = new LPSRequestProfileValidator(lpsRunSetupCommand.LPSRequestProfile);
                requestProfileValidationResults = lpsRequestProfileValidator.Validate();

                if (planValidationResults.IsValid && runValidationResulta.IsValid && requestProfileValidationResults.IsValid)
                {
                    _planSetupCommand.LPSHttpRuns.Add(lpsRunSetupCommand);
                    _planSetupCommand.IsValid = true;
                    string json = LPSSerializationHelper.Serialize(_planSetupCommand);
                    File.WriteAllText($"{testName}.json", json);
                    AnsiConsole.MarkupLine("[Green]Your http run has been added successfully[/]");
                }
                else
                {
                    planValidationResults.PrintValidationErrors();
                    runValidationResulta.PrintValidationErrors();
                    requestProfileValidationResults.PrintValidationErrors();
                }
            },
            LPSCommandLineOptions.LPSAddCommandOptions.TestNameOption,
            new LPSRunCommandBinder());
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
