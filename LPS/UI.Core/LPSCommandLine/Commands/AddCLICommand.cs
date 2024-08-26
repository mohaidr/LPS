using FluentValidation;
using FluentValidation.Results;
using LPS.Domain;
using LPS.Infrastructure.Common;
using LPS.UI.Common;
using LPS.UI.Common.Extensions;
using LPS.UI.Core.LPSCommandLine.Bindings;
using LPS.UI.Core.LPSValidators;
using Spectre.Console;
using System.CommandLine;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace LPS.UI.Core.LPSCommandLine.Commands
{
    internal class AddCLICommand: ICLICommand
    {
        private Command _rootLpsCliCommand;
        private TestPlan.SetupCommand _planSetupCommand;
        private Command _addCommand;
        private string[] _args;
        internal AddCLICommand(Command rootLpsCliCommand, TestPlan.SetupCommand planSetupCommand, string[] args)
        {
            _rootLpsCliCommand = rootLpsCliCommand;
            _planSetupCommand = planSetupCommand;
            _args = args;
            Setup();
        }

        private void Setup()
        {
            _addCommand = new Command("add", "Add an http run");

            CommandLineOptions.AddOptionsToCommand(_addCommand, typeof(CommandLineOptions.LPSAddCommandOptions));
            _rootLpsCliCommand.AddCommand(_addCommand);
        }

        public void Execute(CancellationToken cancellationToken)
        {
            _addCommand.SetHandler((testName, lpsRunSetupCommand) =>
            {
                ValidationResult planValidationResults, runValidationResulta, requestProfileValidationResults;
                _planSetupCommand = SerializationHelper.Deserialize<TestPlan.SetupCommand>(File.ReadAllText($"{testName}.json"));
                var planValidator = new TestPlanValidator(_planSetupCommand);
                planValidationResults = planValidator.Validate();
                var lpsRunValidator = new RunValidator(lpsRunSetupCommand);
                runValidationResulta = lpsRunValidator.Validate();
                var lpsRequestProfileValidator = new RequestProfileValidator(lpsRunSetupCommand.LPSRequestProfile);
                requestProfileValidationResults = lpsRequestProfileValidator.Validate();

                if (planValidationResults.IsValid && runValidationResulta.IsValid && requestProfileValidationResults.IsValid)
                {
                    _planSetupCommand.LPSRuns.Add(lpsRunSetupCommand);
                    _planSetupCommand.IsValid = true;
                    string json = SerializationHelper.Serialize(_planSetupCommand);
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
            CommandLineOptions.LPSAddCommandOptions.TestNameOption,
            new RunCommandBinder());
            _rootLpsCliCommand.Invoke(_args);
        }
    }
}
